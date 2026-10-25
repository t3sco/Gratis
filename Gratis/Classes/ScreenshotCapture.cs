using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using KamiLib.CommandManager;
using Math = global::System.Math;

namespace Gratis.Classes;

public sealed class ScreenshotCapture : IDisposable
{
    private const string OutputDirectory = @"C:\Users\cmaho\Documents\Gratis\_shots";

    [PluginService] private IChatGui ChatGui { get; set; } = null!;

    private readonly CommandHandler command;
    private int busy;
    private int pendingDraw;
    private bool mapOnly;
    private Rect? mapBounds;
    private volatile bool disposed;
    private string? reportOutput;
    private Action<bool>? reportCompletion;
    private Rendering.MapDrawContext? privacyContext;

    public ScreenshotCapture()
    {
        Service.PluginInterface.Inject(this);
        command = new CommandHandler { ActivationPath = "/shot", Delegate = Request };
        System.CommandManager.RegisterCommand(command);
        Service.PluginInterface.UiBuilder.Draw += OnDraw;
    }

    public void Dispose()
    {
        disposed = true;
        Service.PluginInterface.UiBuilder.Draw -= OnDraw;
        System.CommandManager.RemoveCommand(command);
        CompleteReport(false);
    }

    private void Request(params string[] args)
    {
        if (args.Length > 1 || (args.Length == 1 && !args[0].Equals("map", StringComparison.OrdinalIgnoreCase))) {
            ChatGui.PrintError("[Gratis] Usage: /gratis shot [map]");
            return;
        }

        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) {
            ChatGui.PrintError("[Gratis] A screenshot is already pending.");
            return;
        }

        mapOnly = args.Length == 1;
        reportOutput = null;
        mapBounds = null;
        Volatile.Write(ref pendingDraw, 1);
    }

    internal bool RequestReport(string path, Action<bool> completion)
    {
        var ctx = Rendering.MapDrawContext.Current;
        if (disposed || !System.MapWindow.IsOpen || System.MapWindow.PlacePalette.Overlay.IsOpen ||
            ctx.ContextMenu.CapturesInput || ctx.Assistance?.PinLayer?.CapturesInput == true ||
            Interlocked.CompareExchange(ref busy, 1, 0) != 0) return false;
        reportOutput = path; reportCompletion = completion;
        privacyContext = ctx; ctx.ReportCapture = true;
        mapOnly = true; mapBounds = null;
        Volatile.Write(ref pendingDraw, 1);
        return true;
    }

    private void CompleteReport(bool success)
    {
        if (privacyContext is { } ctx) ctx.ReportCapture = false;
        privacyContext = null;
        try { Interlocked.Exchange(ref reportCompletion, null)?.Invoke(success); }
        catch (Exception error) { Service.Log.Warning(error, "[Gratis] Could not update report screenshot status."); }
    }

    private void OnDraw()
    {
        if (disposed || Volatile.Read(ref pendingDraw) == 0) return;

        try {
            if (reportOutput is not null && (System.MapWindow.PlacePalette.Overlay.IsOpen || Rendering.MapDrawContext.Current.ContextMenu.CapturesInput ||
                Rendering.MapDrawContext.Current.Assistance?.PinLayer?.CapturesInput == true))
                throw new InvalidOperationException("A text-entry overlay opened during report capture.");
            if (pendingDraw == 1) {
                if (mapOnly) {
                    var map = System.MapWindow;
                    if (!map.IsOpen || !map.DrawConditions()) throw new InvalidOperationException("Open the Gratis map before using /gratis shot map.");

                    var visible = ImGui.Begin(map.WindowName);
                    try {
                        if (!visible) throw new InvalidOperationException("The Gratis map is collapsed.");
                        var position = ImGui.GetWindowPos() - ImGui.GetMainViewport().Pos;
                        var size = ImGui.GetWindowSize();
                        mapBounds = new Rect {
                            Left = (int)Math.Floor(position.X), Top = (int)Math.Floor(position.Y),
                            Right = (int)Math.Ceiling(position.X + size.X), Bottom = (int)Math.Ceiling(position.Y + size.Y),
                        };
                    }
                    finally {
                        ImGui.End();
                    }
                }

                Volatile.Write(ref pendingDraw, 2);
                return;
            }

            Volatile.Write(ref pendingDraw, 0);
            var bounds = mapBounds;
            _ = Task.Run(() => Capture(bounds));
        }
        catch (Exception exception) {
            Volatile.Write(ref pendingDraw, 0);
            Interlocked.Exchange(ref busy, 0);
            ReportFailure(exception);
        }
    }

    private void Capture(Rect? bounds)
    {
        try {
            if (disposed) return;
            using var process = Process.GetCurrentProcess();
            var window = process.MainWindowHandle;
            if (window == 0 || IsIconic(window)) throw new InvalidOperationException("The game window is unavailable or minimized.");
            if (!GetClientRect(window, out var client)) throw new Win32Exception();

            var crop = bounds is { } map ? new Rect {
                Left = Math.Max(0, map.Left), Top = Math.Max(0, map.Top),
                Right = Math.Min(client.Right, map.Right), Bottom = Math.Min(client.Bottom, map.Bottom),
            } : client;
            if (crop.Right <= crop.Left || crop.Bottom <= crop.Top) throw new InvalidOperationException("The Gratis map is outside the game client area.");

            var info = new BitmapInfo {
                Size = 40, Width = client.Right, Height = -client.Bottom, Planes = 1, BitCount = 32,
            };
            var dc = CreateCompatibleDC(0);
            if (dc == 0) throw new Win32Exception();
            nint bitmap = 0;
            nint previous = 0;
            byte[] pixels;
            try {
                bitmap = CreateDIBSection(dc, ref info, 0, out var bits, 0, 0);
                if (bitmap == 0) throw new Win32Exception();
                previous = SelectObject(dc, bitmap);
                if (previous == 0 || previous == -1) throw new Win32Exception();

                Marshal.ThrowExceptionForHR(DwmFlush());
                if (!PrintWindow(window, dc, 0x3)) throw new InvalidOperationException("PrintWindow could not capture the rendered game client.");
                pixels = new byte[checked(client.Right * client.Bottom * 4)];
                Marshal.Copy(bits, pixels, 0, pixels.Length);
            }
            finally {
                if (previous != 0 && previous != -1) SelectObject(dc, previous);
                if (bitmap != 0) DeleteObject(bitmap);
                DeleteDC(dc);
            }

            if (disposed) return;
            if (reportOutput is { } reportPath) {
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
                WritePng(reportPath, pixels, client.Right, crop);
                CompleteReport(true);
                return;
            }
            Directory.CreateDirectory(OutputDirectory);
            var timestamped = Path.Combine(OutputDirectory, $"shot-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}.png");
            WritePng(timestamped, pixels, client.Right, crop);
            var latest = Path.Combine(OutputDirectory, "latest.png");
            File.Copy(timestamped, latest, true);
            foreach (var old in Directory.EnumerateFiles(OutputDirectory, "shot-*.png").OrderByDescending(Path.GetFileName, StringComparer.Ordinal).Skip(20)) {
                File.Delete(old);
            }

            Service.Log.Information($"[Gratis] Screenshot saved: {latest} (archive: {timestamped})");
            Notify($"[Gratis] Screenshot saved: {latest}");
        }
        catch (Exception exception) {
            ReportFailure(exception);
        }
        finally {
            Interlocked.Exchange(ref busy, 0);
        }
    }

    private void ReportFailure(Exception exception)
    {
        CompleteReport(false);
        Service.Log.Error(exception, "[Gratis] Screenshot capture failed.");
        Notify($"[Gratis] Screenshot failed: {exception.Message}", true);
    }

    private void Notify(string message, bool error = false)
    {
        if (disposed) return;
        _ = Service.Framework.RunOnFrameworkThread(() => {
            if (disposed) return;
            if (error) ChatGui.PrintError(message);
            else ChatGui.Print(message);
        });
    }

    private static void WritePng(string path, byte[] bgra, int stridePixels, Rect crop)
    {
        var width = crop.Right - crop.Left;
        var height = crop.Bottom - crop.Top;
        using var output = File.Create(path);
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8;
        header[9] = 2;
        WriteChunk(output, "IHDR"u8, header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true)) {
            var row = new byte[checked(width * 3 + 1)];
            for (var y = crop.Top; y < crop.Bottom; y++) {
                for (var x = 0; x < width; x++) {
                    var source = ((y * stridePixels) + crop.Left + x) * 4;
                    row[x * 3 + 1] = bgra[source + 2];
                    row[x * 3 + 2] = bgra[source + 1];
                    row[x * 3 + 3] = bgra[source];
                }
                zlib.Write(row);
            }
        }
        WriteChunk(output, "IDAT"u8, compressed.GetBuffer().AsSpan(0, checked((int)compressed.Length)));
        WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        output.Write(number);
        output.Write(type);
        output.Write(data);
        var crc = uint.MaxValue;
        foreach (var value in type) crc = UpdateCrc(crc, value);
        foreach (var value in data) crc = UpdateCrc(crc, value);
        BinaryPrimitives.WriteUInt32BigEndian(number, ~crc);
        output.Write(number);
    }

    private static uint UpdateCrc(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
        return crc;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, SizeImage;
        public int XPelsPerMeter, YPelsPerMeter;
        public uint ClrUsed, ClrImportant, Color;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetClientRect(nint window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern bool PrintWindow(nint window, nint dc, uint flags);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint SelectObject(nint dc, nint item);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint item);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
}
