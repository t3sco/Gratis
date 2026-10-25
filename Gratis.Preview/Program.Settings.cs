using System.Numerics;
using System.Runtime.InteropServices;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class Program
{
    private static unsafe void RenderSettings(Options options)
    {
        var previousNative = ImGui.GetCurrentContext(); var previous = MapDrawContext.Current;
        var native = ImGuiErrors.CreateContext();
        try {
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2(options.Width, options.Height);
            io.DisplayFramebufferScale = Vector2.One;
            io.DeltaTime = 1f / 60;
            io.NativePtr->IniFilename = null;
            io.NativePtr->LogFilename = null;
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
            ImGui.StyleColorsDark();
            var textures = new Textures();
            using var context = new PreviewContext(textures) { DpiScale = options.Dpi };
            MapDrawContext.Current = context;
            context.BuildFonts(Path.Combine(AppContext.BaseDirectory, "assets", "NotoSans-Regular.ttf"));
            io.Fonts.GetTexDataAsRGBA32(out nint pixels, out var width, out var height, out _);
            var atlas = new byte[checked(width * height * 4)];
            Marshal.Copy(pixels, atlas, 0, atlas.Length);
            io.Fonts.SetTexID(textures.Add(width, height, atlas));
            var config = new SystemConfig { VisualStyle = options.Scene == "settings-classic" ? 1 : 0 };
            var layout = new SettingsLayout {
                SelectedSection = options.Scene switch {
                    "settings-filters" => Array.IndexOf(SettingsLayout.Sections, "Filters"), "settings-map" => 1, "settings-markers" => 2, "settings-chrome" => 3,
                    "settings-tools" => 4, "settings-advanced" => 5, _ => 0,
                },
                Search = options.Scene == "settings-search" ? "zoom" : "",
            };
            var windowSize = Vector2.Min(io.DisplaySize - new Vector2(32 * options.Dpi), new Vector2(820, 720) * options.Dpi);
            var historyLimit = 10;
            for (var frame = 0; frame < 4; frame++) {
                ImGui.NewFrame();
                ImGui.SetNextWindowPos((io.DisplaySize - windowSize) / 2);
                ImGui.SetNextWindowSize(windowSize);
                var beforeColors = Enumerable.Range(0, (int)ImGuiCol.COUNT).Select(index => ImGui.GetStyle().Colors[index]).ToArray();
                var beforeMetrics = (ImGui.GetFontSize(), ImGui.GetStyle().WindowPadding, ImGui.GetStyle().FramePadding, ImGui.GetStyle().ItemSpacing);
                var theme = SettingsTheme.Push(config);
                if (ImGui.Begin("Gratis settings", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoCollapse)) {
                    layout.Draw(config, (section, search) => {
                        if (section == "Modules / tools") {
                            SettingsLayout.DrawTools(search, ref historyLimit, null);
                            ImGui.TextDisabled("Tool windows require the game.");
                        }
                        else if (section == "Marker options") {
                            ImGui.TextDisabled("Marker owner controls require the game adapter.");
                        }
                        return false;
                    });
                    if (config.IsGratis) VerifySettingsBounds(layout);
                }
                ImGui.End();
                theme.Pop();
                if (!beforeColors.SequenceEqual(Enumerable.Range(0, (int)ImGuiCol.COUNT).Select(index => ImGui.GetStyle().Colors[index])) ||
                    beforeMetrics != (ImGui.GetFontSize(), ImGui.GetStyle().WindowPadding, ImGui.GetStyle().FramePadding, ImGui.GetStyle().ItemSpacing))
                    throw new InvalidOperationException("Settings theme leaked outside its window.");
                ImGui.Render();
            }
            var renderer = new SoftwareRenderer(options.Width, options.Height, textures);
            renderer.Render(ImGui.GetDrawData(), context.Blurs);
            renderer.Save(options.Output);
            Console.WriteLine("Shared settings layout: seven sections, Lucide headers, search, style disclosure and resets. Game extensions are labelled.");
            if (options.Verify) SettingsVerification.Run();
        }
        finally { ImGuiErrors.DestroyContext(native); ImGui.SetCurrentContext(previousNative); MapDrawContext.Current = previous; }
    }

    private static void VerifySettingsBounds(SettingsLayout layout)
    {
        if (ChromeLayout.Overlap(layout.TestNavigation, layout.TestContent)) throw new InvalidOperationException("Settings navigation overlaps its content.");
        foreach (var row in layout.TestRows) {
            if (ChromeLayout.Overlap(row.Label, row.Control)) throw new InvalidOperationException($"Settings label overlaps its control: {row.Path}");
            if (row.Label.X < layout.TestContent.X - 1 || row.Label.Z > layout.TestContent.Z + 1 ||
                row.Control.X < layout.TestContent.X - 1 || row.Control.Z > layout.TestContent.Z + 1)
                throw new InvalidOperationException($"Settings row overflows the content width: {row.Path}");
        }
    }

    internal static void VerifyResponsiveSettings()
    {
        foreach (var size in new[] { "360x640", "640x720", "1280x900" })
        foreach (var scene in new[] { "settings-chrome", "settings-filters", "settings-search" }) {
            var output = Path.GetFullPath(Path.Combine("_preview", "settings-layout", scene + "-" + size + ".png"));
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            RenderSettings(Options.Parse(["--scene", scene, "--size", size, "--out", output])!);
        }
        Console.WriteLine("Settings layout: nine real-frame small/normal/large pages; icon rail, stacked fields, labels and controls stay within their columns.");
    }
}
