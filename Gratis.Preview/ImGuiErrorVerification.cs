using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace Gratis.Preview;

internal static class ImGuiErrorVerification {
    public static void Run() {
        foreach (var mode in new[] { "imgui-error-injected", "imgui-assert-injected" }) {
            var start = new ProcessStartInfo("dotnet") {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--selftest");
            start.ArgumentList.Add(mode);
            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("Cannot start ImGui diagnostic probe.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(10000)) {
                process.Kill(true);
                throw new InvalidOperationException(mode + " blocked (possible native assertion dialog).");
            }
            var text = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
            if (process.ExitCode == 0
                || !text.Contains(mode == "imgui-error-injected" ? "injected recovery error" : "Code uses SetCursorPos",
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"{mode}: expected headless failure, got {process.ExitCode}: {text}");
        }
        Console.WriteLine("ImGui diagnostics: native callback makes recovered errors fail; hard cursor "
            + "assertion exits headlessly within 10 seconds.");
    }
    public static void Inject(bool hardAssert) {
        using var driver = new ScriptedFrameDriver();
        driver.Step(new ScriptFrame(0));
        var io = ImGui.GetIO();
        if (io.ConfigErrorRecoveryEnableAssert || !io.ConfigErrorRecoveryEnableDebugLog
            || io.ConfigErrorRecoveryEnableTooltip)
            throw new InvalidOperationException("Incorrect native error recovery policy.");
        if (!hardAssert) {
            var path = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "cimgui.dll");
            var error = Marshal.GetDelegateForFunctionPointer<NativeError>(
                NativeLibrary.GetExport(NativeLibrary.Load(path),
                "igErrorLog"));
            error("injected recovery error");
            return;
        }
        ImGui.NewFrame();
        ImGui.Begin("injected cursor extent");
        ImGui.SetCursorPos(new Vector2(10000));
        ImGui.End();
        ImGui.Render();
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte NativeError([MarshalAs(UnmanagedType.LPUTF8Str)] string message);
}