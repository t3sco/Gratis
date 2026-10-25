using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;
using ImGuiNET;

namespace Gratis.Preview;

internal static class GhostVerification {
    private static void Check(bool value, string name) {
        if (!value)
            throw new InvalidOperationException("Ghost: " + name);
    }

    public static void State() {
        var c = new SystemConfig { GhostOpacity = .35f, GhostRestoreDelay = .6f };
        var g = new GhostMode();
        g.Update(c, 0, true, false, false, false);
        Check(!g.ClickThrough && g.Opacity == 1, "default off");
        c.GhostMode = true;
        g.Update(c, 1, true, false, false, false);
        Check(g.ClickThrough && g.Opacity == 1, "immediate click-through, visual ease starts");
        g.Update(c, 1.3, true, false, false, false);
        Check(MathF.Abs(g.Opacity - .35f) < .0001f, "coarse timestep settles");
        g.Update(c, 1.31, true, true, false, false);
        Check(!g.ClickThrough, "rect hover restores input immediately");
        g.Update(c, 1.37, true, true, false, false);
        Check(g.Opacity > .8f && g.Opacity < .95f, "fast eased restoration");
        g.Update(c, 1.6, true, true, false, false);
        Check(g.Opacity == 1, "140ms restoration");
        g.Update(c, 2, true, false, true, false);
        Check(!g.ClickThrough, "focus");
        g.Update(c, 3, true, false, false, true);
        Check(!g.ClickThrough, "popup/drag/palette/card");
        g.Update(c, 4, true, false, false, false, true);
        Check(!g.ClickThrough, "interaction");
        g.Update(c, 5, true, false, false, false, mapKey: true);
        Check(!g.ClickThrough, "map key");
        g.Update(c, 5.3, true, false, false, false);
        Check(!g.ClickThrough, "wake grace");
        g.Update(c, 6, true, false, false, false);
        Check(g.ClickThrough, "moving again");
        g.Update(c, 6.1, false, false, false, false);
        Check(g.ClickThrough, "stationary delay begins");
        g.Update(c, 6.5, false, false, false, false);
        Check(g.ClickThrough, "stationary delay holds");
        g.Update(c, 6.8, false, false, false, false);
        Check(!g.ClickThrough, "coarse stationary restoration");
        g.Update(c, 7.1, false, false, false, false);
        Check(g.Opacity == 1, "stationary settled");
        c.ReduceMotion = true;
        g.Update(c, 8, true, false, false, false);
        Check(g.Opacity == .35f, "reduce motion");
        c.GhostOpacity = float.NaN;
        g.Update(c, 9, true, false, false, false);
        Check(g.Opacity == .35f, "invalid config fallback");
        g.Update(c, double.NaN, false, false, false, false);
        Check(float.IsFinite(g.Opacity), "invalid clock ignored");
        g.Update(c, 0, false, false, false, false);
        Check(!g.ClickThrough && g.Opacity == 1, "clock rewind");
        c.VisualStyle = 1;
        g.Update(c, 10, true, false, false, false);
        Check(!g.ClickThrough && g.Opacity == 1, "Classic unchanged");
        c.GhostMode = true;
        SettingsLayout.Entries.Single(e => e.Path == "GhostMode").Reset(c, new());
        Check(!c.GhostMode, "settings reset");
    }

    internal static SystemConfig Config() => new() {
        AcceptedSpoilerWarning = true, GhostMode = true, GhostHintSeen = true, GhostOpacity = .35f, GhostRestoreDelay = .6f,
        WindowPosition = new(32), WindowSize = new(640, 480), FadeMode = FadeMode.Always, FadePercent = .1f
    };
    public static void Frames() {
        using var d = new ScriptedFrameDriver(Config(), new FrameScript { Palette = true });
        d.Step(new(0, Focused: false, Moving: true));
        var ghost = d.Step(new(.3, Moving: true));
        Check(ghost.GhostClickThrough && MathF.Abs(ghost.GhostOpacity - .35f) < .0001f, "moving frame");
        Check((d.Flags & ImGuiWindowFlags.NoInputs) == ImGuiWindowFlags.NoInputs, "real window NoInputs");
        Check(MapWindowFrame.GhostInputFlags == ImGuiWindowFlags.NoInputs, "binding flag composition");
        var hover = d.Step(new(.31, Mouse: new(350, 250), Moving: true));
        Check(!hover.GhostClickThrough && (d.Flags & ImGuiWindowFlags.NoMouseInputs) == 0,
            "rect hover removes input flags");
        var restored = d.Step(new(.5, Moving: true));
        Check(restored.GhostOpacity == 1, "FadePercent does not double-fade");
        d.Step(new(.6, Mouse: new(-1000), Moving: true));
        d.Step(new(.8, Moving: true));
        d.Palette!.Open();
        var palette = d.Step(new(.9, Moving: true));
        Check(!palette.GhostClickThrough, "palette blocks ghost before Begin");
        d.Palette.Close(false);
        d.Step(new(1.1, Moving: true));
        var key = d.Step(new(1.3, Moving: true, MapKey: true));
        Check(!key.GhostClickThrough, "scripted map key");
        d.Config.GhostMode = false;
        var off = d.Step(new(2, Moving: true));
        Check(!off.GhostClickThrough && off.GhostOpacity == 1, "disabled resets input");
    }
}