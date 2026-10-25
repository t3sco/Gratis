using System.Numerics;
using System.Runtime.InteropServices;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class Program {
    private static unsafe void RenderSettings(Options options, Action<SettingsLayout, SystemConfig, int>? input = null,
        int frames = 4) {
        var previousNative = ImGui.GetCurrentContext();
        var previous = MapDrawContext.Current;
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
            if (options.Scene == "window-presets")
                config = WindowPresetVerification.SceneConfig();
            var layout = new SettingsLayout {
                SelectedSection = options.Scene switch {
                    "window-presets" => 1,
                    "tweaks-page" or "tweaks-page-search" or "tweaks-whatsnew" =>
                        Array.IndexOf(SettingsLayout.Sections, "Tweaks"),
                    "settings-filters" => Array.IndexOf(SettingsLayout.Sections, "Filters"),
                    "settings-map" => 1,
                    "settings-markers" => 2,
                    "settings-chrome" => 3,
                    "settings-tools" => 4,
                    "settings-advanced" => 5,
                    _ => 0,
                },
                Search = options.Scene switch {
                    "window-presets" => "window presets",
                    "tweaks-page-search" => "map",
                    "settings-search" => "zoom",
                    _ => "",
                },
                WhatsNewOpen = options.Scene == "tweaks-whatsnew",
            };
            var windowSize = Vector2.Min(io.DisplaySize - new Vector2(32 * options.Dpi),
                new Vector2(820, 720) * options.Dpi);
            var historyLimit = 10;
            for (var frame = 0; frame < frames; frame++) {
                input?.Invoke(layout, config, frame);
                ImGui.NewFrame();
                ImGui.SetNextWindowPos((io.DisplaySize - windowSize) / 2);
                ImGui.SetNextWindowSize(windowSize);
                var beforeColors = Enumerable.Range(0,
                    (int)ImGuiCol.COUNT).Select(index => ImGui.GetStyle().Colors[index]).ToArray();
                var beforeMetrics = (ImGui.GetFontSize(), ImGui.GetStyle().WindowPadding, ImGui.GetStyle().FramePadding,
                    ImGui.GetStyle().ItemSpacing);
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
                    if (config.IsGratis)
                        VerifySettingsBounds(layout);
                }
                ImGui.End();
                theme.Pop();
                if (!beforeColors.SequenceEqual(Enumerable.Range(0,
                    (int)ImGuiCol.COUNT).Select(index => ImGui.GetStyle().Colors[index]))
                    || beforeMetrics != (ImGui.GetFontSize(), ImGui.GetStyle().WindowPadding,
                        ImGui.GetStyle().FramePadding, ImGui.GetStyle().ItemSpacing))
                    throw new InvalidOperationException("Settings theme leaked outside its window.");
                ImGui.Render();
            }
            var renderer = new SoftwareRenderer(options.Width, options.Height, textures);
            renderer.Render(ImGui.GetDrawData(), context.Blurs);
            renderer.Save(options.Output);
            Console.WriteLine($"Shared settings layout: {SettingsLayout.Sections.Length} sections, "
                + $"{TweakCatalog.All.Count} tweaks, Lucide headers, search, style disclosure and "
                + "resets. Game extensions are labelled.");
            if (options.Verify)
                SettingsVerification.Run();
        }
        finally {
            ImGuiErrors.DestroyContext(native); ImGui.SetCurrentContext(previousNative); MapDrawContext.Current =
            previous;
        }
    }

    private static void VerifySettingsBounds(SettingsLayout layout) {
        if (ChromeLayout.Overlap(layout.TestNavigation, layout.TestContent))
            throw new InvalidOperationException("Settings navigation overlaps its content.");
        foreach (var row in layout.TestRows) {
            if (ChromeLayout.Overlap(row.Label, row.Control))
                throw new InvalidOperationException($"Settings label overlaps its control: {row.Path}");
            if (row.Label.X < layout.TestContent.X - 1 || row.Label.Z > layout.TestContent.Z + 1
                || row.Control.X < layout.TestContent.X - 1 || row.Control.Z > layout.TestContent.Z + 1)
                throw new InvalidOperationException($"Settings row overflows the content width: {row.Path}");
        }
    }

    internal static void VerifyResponsiveSettings() {
        foreach (var size in new[] { "360x640", "640x720", "1280x900" })
            foreach (var scene in new[] { "settings-chrome", "settings-filters", "settings-search" }) {
                var output = Path.GetFullPath(Path.Combine("_preview", "settings-layout", scene + "-" + size + ".png"));
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                RenderSettings(Options.Parse(["--scene", scene, "--size", size, "--out", output])!);
            }
        Console.WriteLine("Settings layout: nine real-frame small/normal/large pages; icon rail, stacked "
            + "fields, labels and controls stay within their columns.");
    }

    internal static void VerifyTweakPages() {
        foreach (var scene in new[] { "tweaks-page", "tweaks-page-search", "tweaks-whatsnew" })
            foreach (var size in new[] { "360x640", "820x800" }) {
                var output = Path.GetFullPath(Path.Combine("_preview", "tweaks", scene + "-" + size + ".png"));
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                RenderSettings(Options.Parse(["--scene", scene, "--size", size, "--out", output])!);
            }
        static void Click(SettingsLayout layout, string id, bool down) {
            var point = layout.TestTweakActions[id];
            ImGui.GetIO().AddMousePosEvent(point.X, point.Y);
            ImGui.GetIO().AddMouseButtonEvent(0, down);
        }
        var testOutput = Path.GetFullPath(Path.Combine("_preview", "tweaks", "actions.png"));
        RenderSettings(Options.Parse(["--scene", "tweaks-page", "--out", testOutput])!, (layout, config, frame) => {
            switch (frame) {
                case 0:
                    layout.Search = "ghost";
                    break;
                case 2:
                    Click(layout, "ghost/toggle", true);
                    break;
                case 3:
                    Click(layout, "ghost/toggle", false);
                    break;
                case 4:
                    if (!config.GhostMode)
                        throw new InvalidOperationException("Real tweak toggle did not change config.");
                    Click(layout, "modified", true);
                    break;
                case 5:
                    Click(layout, "modified", false);
                    break;
                case 6:
                    if (!layout.ModifiedTweaksOnly)
                        throw new InvalidOperationException("Modified-only toggle did not activate.");
                    Click(layout, "ghost/reset", true);
                    break;
                case 7:
                    Click(layout, "ghost/reset", false);
                    break;
                case 8:
                    if (config.GhostMode || TweakCatalog.Find(config, layout.Search, true).Length != 0)
                        throw new InvalidOperationException("Real per-item reset did not clear modified result.");
                    layout.ModifiedTweaksOnly = false;
                    layout.Search = "flag arrival";
                    break;
                case 10:
                    Click(layout, "flag-arrival/configure", true);
                    break;
                case 11:
                    Click(layout, "flag-arrival/configure", false);
                    break;
                case 12:
                    if (SettingsLayout.Sections[layout.SelectedSection] != "Chrome & motion"
                        || layout.Search != "Motion")
                        throw new InvalidOperationException(
                            "Real Configure click did not route to the existing group.");
                    layout.SelectedSection = Array.IndexOf(SettingsLayout.Sections, "Tweaks");
                    layout.Search = "";
                    layout.WhatsNewOpen = true;
                    break;
                case 14:
                    Click(layout, "dismiss-news", true);
                    break;
                case 15:
                    Click(layout, "dismiss-news", false);
                    break;
                case 16:
                    if (TweakNews.HasUnseen(config))
                        throw new InvalidOperationException("Real news dismissal did not clear the badge.");
                    break;
            }
        }, frames: 18);
    }
}