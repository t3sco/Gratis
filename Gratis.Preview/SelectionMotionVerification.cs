using System.Numerics;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class SelectionMotionVerification {
    private static void Check(bool value, string message) {
        if (!value)
            throw new InvalidOperationException("Selection motion: " + message);
    }

    // The sliding selection bar is animated in window space, so dragging the window carries it along instead
    // of leaving it behind to catch up.
    public static void FollowsWindow() {
        using var driver = new ScriptedFrameDriver();
        driver.Step(new(0));
        var rest = new Vector2(12, 40);
        Vector2 Frame(Vector2 window, Vector2 row) {
            ImGui.GetIO().DeltaTime = 1 / 60f;
            ImGui.NewFrame();
            ImGui.SetNextWindowPos(window);
            ImGui.SetNextWindowSize(new(300, 200));
            ImGui.Begin("selection-motion");
            var result = UiMotion.Selection("##selection-motion", window + row);
            ImGui.End();
            ImGui.Render();
            return result;
        }
        for (var i = 0; i < 10; i++)
            Frame(new(100, 100), rest);
        var worst = 0f;
        for (var i = 0; i < 40; i++) {
            var window = new Vector2(100 + i * 14, 100 + i * 5);
            worst = MathF.Max(worst, Vector2.Distance(Frame(window, rest), window + rest));
        }
        Check(worst < .5f, $"a dragged window keeps the bar on its row (worst gap {worst:0.##}px)");
        var gap = Vector2.Distance(Frame(new(660, 300), new(12, 120)), new Vector2(660, 300) + new Vector2(12, 120));
        Check(gap > 1, "moving the target within the window still animates");
        for (var i = 0; i < 60; i++)
            Frame(new(660, 300), new(12, 120));
        Check(Vector2.Distance(Frame(new(660, 300), new(12, 120)), new Vector2(660, 300) + new Vector2(12, 120)) < .5f,
            "the bar settles on the new row");
        Console.WriteLine("Selection motion: a dragged window carries the bar, row changes still ease, and it settles.");
    }
}
