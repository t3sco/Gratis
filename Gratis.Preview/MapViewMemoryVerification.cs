using System.Numerics;
using System.Text.Json;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;

namespace Gratis.Preview;

internal static class MapViewMemoryVerification
{
    private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException("View memory: " + name); }
    public static void State()
    {
        var c = JsonSerializer.Deserialize<SystemConfig>("{}", ScriptedFrameDriver.Json)!;
        var m = new MapViewMemory(); var size = new Vector2(640, 480); var texture = new Vector2(2048);
        Check(c.RememberMapView && !c.RememberMapLens && c.MapViews.Count == 0, "old config defaults");
        Check(m.Find(c, 1) is null, "missing map");
        c.FollowPlayer = false;
        Check(m.Capture(c, 1, 1, new(40, -20), size), "capture");
        Check(!m.Capture(c, 1, 1, new(40, -20), size), "unchanged capture");
        Check(m.Find(c, 1)!.Offset == new Vector2(40, -20), "unfollowed pan");
        c.FollowPlayer = true; m.Capture(c, 1, 1, new(80), size);
        Check(m.Find(c, 1)!.Offset is null, "following never stores pan");
        c.ActiveLens = "Travel"; m.Capture(c, 1, 1, default, size);
        Check(m.Find(c, 1)!.Lens == "Travel", "lens");
        Check(m.Find(c, 1)!.ResolveScale(c, size * 2, texture) == 2, "frame-size change");
        c.ScaleWithFrame = false;
        Check(m.Find(c, 1)!.ResolveScale(c, size * 2, texture) == 1, "absolute zoom");
        Check(m.Find(c, 1)!.ResolveScale(c, new(3000), texture) >= 3000f / 2048, "clamp minimum");
        c.ClampToMap = false;
        Check(m.Find(c, 1)!.ResolveScale(c, new(3000), texture) == 1, "unclamped");
        Check(!m.Capture(c, 0, 1, default, size), "zero map");
        Check(!m.Capture(c, 2, float.NaN, default, size), "NaN scale");
        Check(!m.Capture(c, 2, -1, default, size), "negative scale");
        Check(!m.Capture(c, 2, 1, new(float.PositiveInfinity), size), "infinite pan");
        for (uint i = 2; i <= 100; i++) m.Capture(c, i, 1, default, size);
        Check(c.MapViews.Count == 100, "capacity");
        m.Find(c, 1, true); m.Capture(c, 101, 1, default, size);
        Check(m.Find(c, 2) is null && m.Find(c, 1) is not null, "LRU touch and eviction");
        var roundtrip = JsonSerializer.Deserialize<SystemConfig>(JsonSerializer.Serialize(c, ScriptedFrameDriver.Json), ScriptedFrameDriver.Json)!;
        Check(new MapViewMemory().Find(roundtrip, 1)!.Lens == "Travel", "persisted roundtrip");
        roundtrip.MapViews = null!;
        Check(new MapViewMemory().Find(roundtrip, 1) is null && roundtrip.MapViews.Count == 0, "explicit null config");
        roundtrip.MapViews = [null!, new() { MapId = 0 }, new() { MapId = 7, Scale = float.NaN },
            new() { MapId = 8, FrameExtent = 0 }, new() { MapId = 9, Offset = new(float.NaN), Lens = null! }, new() { MapId = 9, Scale = 2 }];
        var safe = new MapViewMemory();
        Check(safe.Find(roundtrip, 9)!.Scale == 2 && roundtrip.MapViews.Count == 1, "invalid entries and duplicate last wins");
        roundtrip.MapViews = [new() { MapId = 9, Offset = new(float.NaN), Lens = null! }];
        Check(safe.Find(roundtrip, 9) is { Offset: null, Lens: "Explore" }, "sanitize invalid pan and lens");
        c.RememberMapView = c.RememberMapLens = false;
        Check(!m.Capture(c, 3, 1, default, size), "disabled");
        c.RememberMapLens = true;
        Check(m.Capture(c, 3, 2, default, size), "lens-only memory");
        c.VisualStyle = 1;
        Check(!m.Capture(c, 4, 1, default, size), "Classic unchanged");
        c.RememberMapView = false;
        SettingsLayout.Entries.Single(e => e.Path == "RememberMapView").Reset(c, new());
        Check(c.RememberMapView, "catalog reset");
        Console.WriteLine("25 map-view state cases passed.");
    }

    public static void Frames()
    {
        var config = new SystemConfig { AcceptedSpoilerWarning = true, FadeMode = 0, FollowPlayer = false,
            WindowPosition = new(32), WindowSize = new(640, 480), MapScale = 1, RememberMapLens = true, ReduceMotion = true };
        using var d = new ScriptedFrameDriver(config, new FrameScript { DrawOffset = new(80, -50), Palette = true });
        d.Step(new(0)); d.Config.ActiveLens = "Travel"; var a = d.Step(new(.1));
        d.Step(new(.2, MapId: 2)); d.Renderer.DrawOffset = new(-130, 60); d.Config.MapScale = 1.5f; d.Config.ActiveLens = "Gather";
        d.Step(new(.3)); var restored = d.Step(new(.4, MapId: 1, Redraws: 2));
        Check(restored.Scale == a.Scale && restored.Offset == a.Offset && config.ActiveLens == "Travel", "manual map return and lens");
        Check(restored.MotionAdvances == 1, "multiple layers restore once");
        var resized = d.Step(new(.5, MapId: 2, Size: new Vector2(960, 720)));
        Check(MathF.Abs(resized.Scale - 2.25f) < .001f && resized.Offset == new Vector2(-130, 60), "return at new frame size");
        d.Step(new(.6, MapId: 1, QuestLink: new(new(300, -200), 1)));
        d.Step(new(.8)); d.Step(new(.9, Escape: true));
        var end = d.Step(new(1.3));
        Check(MathF.Abs(end.Scale - 1.5f) < .001f && end.Offset == a.Offset, "map-switch Spotlight returns remembered view");
        config.FollowPlayer = true; config.FollowReturnToPlayer = false;
        var followed = d.Step(new(1.4, MapId: 2, Player: new(35, 20)));
        Check(followed.Offset != new Vector2(-130, 60) && config.FollowPlayer, "follow wins over saved pan");
        config.RememberMapView = config.RememberMapLens = false;
        d.Renderer.DrawOffset = new(20); var disabled = d.Step(new(1.5, MapId: 1));
        Check(disabled.Scale == followed.Scale, "disabled memory does not restore");
    }
}
