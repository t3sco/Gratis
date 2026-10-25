using System.Numerics;
using Gratis.Classes;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class ChromeCornerVerification {
    public static void Run() {
        using var driver = new ScriptedFrameDriver(ChromeLayoutVerification.Config(new(264, 192)),
            new FrameScript { Width = 320, Height = 240 });
        var pixels = Enumerable.Repeat((byte)255, 16 * 16 * 4).ToArray();
        var texture = new DrawTexture(driver.Textures.Add(16, 16, pixels), new(256));
        var cases = 0;
        foreach (var circle in new[] { false, true })
            foreach (var feather in new[] { 0f, 6f, 48f })
                foreach (var offset in new[] {
            new Vector2(-8),
            new Vector2(0),
            new Vector2(4, 0),
            new Vector2(0, 4),
            new Vector2(4),
            new Vector2(36, -20),
            new Vector2(-20, 36) }) {
                    driver.Config.MapCircle = circle;
                    driver.Config.FlatCornerRadius = 32;
                    driver.Config.FeatherEnabled = feather > 0;
                    driver.Config.FeatherWidth = feather;
                    ImGui.NewFrame();
                    ImGui.SetNextWindowPos(new(28, 24));
                    ImGui.SetNextWindowSize(new(264, 192));
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
                    ImGui.Begin("corner clipping", ImGuiWindowFlags.NoDecoration
                            | ImGuiWindowFlags.NoBackground
                            | ImGuiWindowFlags.NoSavedSettings);
                    ImGui.PopStyleVar(2);
                    MapShape.Update(ImGui.GetWindowSize());
                    var min = ImGui.GetWindowPos();
                    var max = min + ImGui.GetWindowSize();
                    var imageMin = min + offset;
                    ImGui.SetCursorScreenPos(imageMin);
                    MapTextureDrawing.DrawMapImage(texture, 1);
                    ImGui.End();
                    ImGui.Render();
                    var renderer = new SoftwareRenderer(320, 240, driver.Textures);
                    renderer.Render(ImGui.GetDrawData());
                    var background = renderer.Pixel(0, 0);
                    var visible = 0;
                    for (var y = 0; y < 240; y++)
                        for (var x = 0; x < 320; x++) {
                            var point = new Vector2(x + .5f, y + .5f);
                            var half = (max - min) / 2;
                            var q = Vector2.Abs(point - (min + max) / 2) - half + new Vector2(32);
                            var depth = circle ? 96 - Vector2.Distance(point, (min + max) / 2)
                                : 32 - Vector2.Max(q, Vector2.Zero).Length() - MathF.Min(MathF.Max(q.X, q.Y), 0);
                            if (depth < -.2f && renderer.Pixel(x, y) != background)
                                throw new InvalidOperationException(
                                    $"Texture escaped the window mask: {circle}/{feather}/{offset} at {x},{y}");
                            if (depth > MathF.Max(1, feather) + 1 && point.X > imageMin.X && point.Y > imageMin.Y
                                && point.X < imageMin.X + 256 && point.Y < imageMin.Y + 256) {
                                if (renderer.Pixel(x, y).X < .999f)
                                    throw new InvalidOperationException("Clipping removed opaque map interior");
                                visible++;
                            }
                        }
                    if (visible == 0)
                        throw new InvalidOperationException("Corner fixture must contain visible map pixels");
                    cases++;
                }
        Console.WriteLine($"Corner clipping: {cases} pixel-checked texture-bound/offset cases, rounded and "
            + "circular, feather on/off/wide; no map pixels outside the window mask.");
    }
}