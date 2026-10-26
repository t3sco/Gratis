using System.Numerics;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class StackedHoverVerification {
    private static void Require(bool value, string message) {
        if (!value)
            throw new InvalidOperationException(message);
    }

    public static void Run() {
        var config = ChromeLayoutVerification.Config(new(780, 600));
        using var driver = new ScriptedFrameDriver(config, new FrameScript {
            Width = 1000, Height = 800,
            Markers = [new(60412, new(0, 0)), new(60453, new(2, 1)), new(71001, new(-1, 2)), new(60412, new(300, 0)),
                new(60421, new(-300, 0)), new(60421, new(-300, 0))] });
        driver.Markers[0].PrimaryText = () => "Cagalli Yula-attha";
        driver.Markers[1].PrimaryText = () => "Fall of Gentlemen";
        driver.Markers[2].PrimaryText = () => "The next adventure";
        driver.Markers[3].PrimaryText = () => "Somewhere else";
        driver.Markers[4].PrimaryText = () => "First member";
        driver.Markers[5].PrimaryText = () => "Second member";
        driver.Markers[4].DataId = driver.Markers[5].DataId = null;
        double t = 0;
        void Step(Vector2? mouse = null) => driver.Step(new(t += .1, Mouse: mouse ?? new(-1000)));
        Step();
        Step();
        var together = driver.Renderer.WorldToScreen(new(0, 0), driver.Window.MapDrawOffset);
        for (var i = 0; i < 4; i++)
            Step(together);
        Require(HoverCardDrawing.Rows == 3, $"Markers under the pointer share one card, one row each: {HoverCardDrawing.Rows}");
        var alone = driver.Renderer.WorldToScreen(new(300, 0), driver.Window.MapDrawOffset);
        for (var i = 0; i < 4; i++)
            Step(alone);
        Require(HoverCardDrawing.Rows == 1, $"A lone marker still gets a single-row card: {HoverCardDrawing.Rows}");
        var party = driver.Renderer.WorldToScreen(new(-300, 0), driver.Window.MapDrawOffset);
        for (var i = 0; i < 4; i++)
            Step(party);
        Require(HoverCardDrawing.Rows == 2,
            $"Overlapping party members keep a row each even though they share a key: {HoverCardDrawing.Rows}");
    }
}
