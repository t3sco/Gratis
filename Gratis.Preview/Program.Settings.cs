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
        var native = ImGui.CreateContext();
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
            using var context = new PreviewContext(textures);
            MapDrawContext.Current = context;
            context.BuildFonts(Path.Combine(AppContext.BaseDirectory, "assets", "NotoSans-Regular.ttf"));
            io.Fonts.GetTexDataAsRGBA32(out nint pixels, out var width, out var height, out _);
            var atlas = new byte[checked(width * height * 4)];
            Marshal.Copy(pixels, atlas, 0, atlas.Length);
            io.Fonts.SetTexID(textures.Add(width, height, atlas));
            var config = new SystemConfig { VisualStyle = options.Scene == "settings-classic" ? 1 : 0 };
            var layout = new SettingsLayout {
                SelectedSection = options.Scene switch {
                    "settings-map" => 1, "settings-markers" => 2, "settings-chrome" => 3,
                    "settings-tools" => 4, "settings-advanced" => 5, _ => 0,
                },
                Search = options.Scene == "settings-search" ? "zoom" : "",
            };
            var windowSize = Vector2.Min(io.DisplaySize - new Vector2(32), new Vector2(820, 720));
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
            Console.WriteLine("Shared settings layout: six sections, Lucide headers, search, style disclosure and resets. Game extensions are labelled.");
            if (options.Verify) SettingsVerification.Run();
        }
        finally { ImGui.DestroyContext(native); }
    }
}
