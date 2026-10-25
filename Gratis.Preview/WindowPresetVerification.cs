using System.Numerics;
using System.Text.Json;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;
using ImGuiNET;

namespace Gratis.Preview;

internal static class WindowPresetVerification {
    private static void Check(bool condition, string message) {
        if (!condition)
            throw new InvalidOperationException("Window presets: " + message);
    }

    private static bool Near(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < .002f;
    internal static SystemConfig SceneConfig() {
        var c = PaletteVerification.Config();
        var screen = new MoveWorkArea(Vector2.Zero, new(1920, 1080));
        c.WindowPresets.Presets = [
            WindowPresetMath.Capture(1, "Everyday", new(new(100, 80), new(780, 600)), screen),
            WindowPresetMath.Capture(2, "Compact", new(new(1300, 40), new(500, 400)), screen),
            WindowPresetMath.Capture(3, "Duty", new(new(20, 20), new(620, 800)), screen),
        ];
        c.WindowPresets.OpenAt = 1;
        c.WindowPresets.DutyOpenAt = 3;
        return c;
    }

    public static void State() {
        var minimum = new Vector2(400, 250);
        foreach (var origin in new[] { Vector2.Zero, new Vector2(-1920, -300), new Vector2(2560, 80) })
            foreach (var size in new[] { new Vector2(1920, 1080), new Vector2(2560, 1440), new Vector2(960, 640) }) {
                var area = new MoveWorkArea(origin, origin + size);
                var geometry = new WindowGeometry(origin + size * .1f, size * .6f);
                var p = WindowPresetMath.Capture(1, "Everyday", geometry, area);
                var restored = WindowPresetMath.Restore(p, area, minimum);
                Check(Near(restored.Position, geometry.Position) && Near(restored.Size, geometry.Size),
                    "fraction / pixel roundtrip at monitor origin");
                var smaller = new MoveWorkArea(origin, origin + size / 2);
                var clamped = WindowPresetMath.Restore(p, smaller, minimum * 2);
                Check(clamped.Position.X >= smaller.Min.X && clamped.Position.Y >= smaller.Min.Y
                    && clamped.Position.X + clamped.Size.X <= smaller.Max.X + .002f
                    && clamped.Position.Y + clamped.Size.Y <= smaller.Max.Y + .002f,
                    "resolution / DPI change keeps the entire window on screen, including screens "
                        + "smaller than the minimum");
                Check(clamped.Size.X >= Math.Min(minimum.X * 2, size.X / 2)
                    && clamped.Size.Y >= Math.Min(minimum.Y * 2, size.Y / 2),
                    "chrome minimum");
            }
        var legacy = JsonSerializer.Deserialize<SystemConfig>(
            "{\"WindowPosition\":{\"X\":17,\"Y\":29},\"WindowSize\":{\"X\":700,\"Y\":480},\"VisualStyle\":1}",
            ScriptedFrameDriver.Json)!;
        Check(legacy.WindowPresetOptions.Presets.Count == 0 && legacy.WindowPresets.OpenAt == 0
            && legacy.WindowPresets.DutyOpenAt == 0 && legacy.WindowPosition == new Vector2(17, 29)
            && legacy.VisualStyle == 1,
            "old configs retain geometry / Classic and default to last position");
        var nulls = JsonSerializer.Deserialize<SystemConfig>("{\"WindowPresets\":null}", ScriptedFrameDriver.Json)!;
        Check(nulls.WindowPresetOptions.Presets.Count == 0, "explicit null settings");
        nulls.WindowPresets.Presets = null!;
        Check(nulls.WindowPresetOptions.Presets.Count == 0, "explicit null list");
        var c = SceneConfig();
        var state = new WindowPresetController();
        var screen = new MoveWorkArea(Vector2.Zero, new(1920, 1080));
        Check(c.WindowPresets.ForContext(false) == 1 && c.WindowPresets.ForContext(true) == 3, "duty selection");
        Check(!state.Diagnostics(c, true).Contains("Everyday"), "diagnostics omit user names");
        state.OnOpen(c.WindowPresets, true);
        Check(!state.ApplyPending(c, screen, minimum, true) && state.Pending == 3, "held gesture defers apply");
        Check(state.ApplyPending(c, screen, minimum, false) && c.WindowSize == new Vector2(620, 800),
            "release applies selected duty preset");
        c.WindowPosition = new(400, 150);
        c.WindowSize = new(900, 700);
        Check(!state.ApplyPending(c, screen, minimum, false) && c.WindowSize == new Vector2(900, 700),
            "manual movement persists during this opening");
        state.Cancel();
        state.OnOpen(c.WindowPresets, false);
        Check(state.ApplyPending(c, screen, minimum, false) && Near(c.WindowPosition, new(100, 80))
            && Near(c.WindowSize, new(780, 600)),
            "close / reopen restores preset");
        c.WindowPresets.OpenAt = 0;
        state.Cancel();
        state.OnOpen(c.WindowPresets, false);
        Check(!state.ApplyPending(c, screen, minimum, false), "last position does not move the window");
        c.WindowPresets.OpenAt = 2;
        c.WindowPresets.Delete(3);
        Check(c.WindowPresets.DutyOpenAt == 0 && c.WindowPresets.ForContext(true) == 2,
            "delete duty assignment falls back");
        c.WindowPresets.Delete(1);
        Check(c.WindowPresets.Find(2)?.Name == "Compact" && c.WindowPresets.OpenAt == 2,
            "stable slots after earlier deletion");
        c.WindowPresets.Find(2)!.Name = "Renamed";
        var roundtrip = JsonSerializer.Deserialize<SystemConfig>(JsonSerializer.Serialize(c, ScriptedFrameDriver.Json),
            ScriptedFrameDriver.Json)!;
        Check(roundtrip.WindowPresetOptions.OpenAt == 2 && roundtrip.WindowPresets.Find(2)?.Name == "Renamed",
            "saved settings roundtrip");
        var invalid = new WindowPresetSettings {
            OpenAt = 5, DutyOpenAt = 99, Presets = [null!, new() { Slot = 99 }, new() {
            Slot = 1,
            Size = new(float.NaN) },
            new() { Slot = 2, Size = new(.3f), Name = "\n\u202e" }, new() { Slot = 2, Size = new(.4f) }]
        };
        invalid.Normalize();
        Check(invalid.Presets.Count == 1 && invalid.Find(2)!.Name == "Preset 2" && invalid.OpenAt == 0
            && invalid.DutyOpenAt == 0,
            "malformed entries / names / assignments migrate safely");
        var bad = WindowPresetMath.Clamp(new(new(float.NaN), new(float.PositiveInfinity)), screen, minimum);
        Check(bad.Position == screen.Min && bad.Size == minimum, "invalid geometry fallback");
        SettingsLayout.Entries.Single(e => e.Path == "WindowPresets").Reset(c, new());
        Check(c.WindowPresetOptions.Presets.Count == 0 && c.WindowPresets.OpenAt == 0
            && c.WindowPresets.DutyOpenAt == 0,
            "catalog reset");
        Console.WriteLine("Window presets: fractions, monitor offsets, resolution / DPI clamps, minimums, "
            + "apply-on-open, duty selection, stable deletion, rename, old / null / invalid "
            + "configs, persistence and reset passed.");
    }

    public static void Frames() {
        foreach (var classic in new[] { false, true }) {
            var c = PaletteVerification.Config();
            c.VisualStyle = classic ? 1 : 0;
            c.ClassicMoveHandle = classic;
            using var d = new ScriptedFrameDriver(c, new FrameScript { Width = 1280, Height = 900, Palette = true });
            for (var i = 0; i < 5; i++)
                d.Step(new(i * .02));
            var controller = d.Context.WindowPresets;
            Check(controller.SaveCurrent(c, "Everyday"), "save from shared host");
            var saved = new WindowGeometry(c.WindowPosition, c.WindowSize);
            c.WindowPosition = new(100, 80);
            c.WindowSize = new(800, 650);
            controller.Request(1);
            var applied = d.Step(new(.12));
            Check(Near(applied.WindowPosition, saved.Position) && Near(applied.WindowSize, saved.Size),
                "pre-Begin apply in each style");
            var handle = applied.HandleCenter;
            d.Step(new(.14, Mouse: handle));
            d.Step(new(.16, Left: true));
            d.Step(new(.18, Mouse: handle + new Vector2(70, 50)));
            controller.Request(1);
            var dragged = d.Step(new(.20, Mouse: handle + new Vector2(90, 50)));
            Check(controller.Pending == 1 && dragged.MovingWindow && dragged.WindowPosition != saved.Position,
                "never apply during handle drag");
            d.Step(new(.22, Left: false));
            var released = d.Step(new(.24));
            Check(controller.Pending == 0 && Near(released.WindowPosition, saved.Position),
                "queued apply after gesture ends");
            var mapPoint = c.WindowPosition + c.WindowSize / 2;
            d.Step(new(.242, Mouse: mapPoint));
            d.Step(new(.244, Left: true));
            d.Step(new(.246, Mouse: mapPoint + new Vector2(30, 20)));
            d.Window.ResetWindowInput();
            controller.Cancel();
            c.WindowPresets.OpenAt = 1;
            controller.OnOpen(c.WindowPresets, false);
            var reopened = d.Step(new(.248, Left: false));
            Check(!d.Window.Dragging && controller.Pending == 0 && Near(reopened.WindowPosition, saved.Position),
                "close during pan / reopen clears stale input");
            for (var i = 2; i <= 5; i++)
                Check(controller.SaveCurrent(c, "Preset " + i), "five slots");
            Check(!controller.SaveCurrent(c) && controller.Commands(c).Count() == 5, "capacity removes save command");
            Check(c.ClampToMap == false && c.ScaleWithFrame == false && c.MoveHandle,
                "existing movement / viewport policies unchanged");
            if (classic)
                continue;
            d.Palette!.Open("Map: Everyday");
            d.Step(new(.26));
            d.Step(new(.28));
            Check(d.Palette.Results.Any(r => r.Place.Id == "action:window:1"), "palette indexes named commands");
            d.Step(new(.30, Keys: [ImGuiKey.Enter]));
            d.Step(new(.32));
            Check(!d.Palette.IsOpen && controller.LastApplied == 1, "palette Enter executes preset action");
            c.WindowPresets.Delete(5);
            controller.Changed();
            d.Palette.Open("Save current as preset");
            d.Step(new(.34));
            d.Step(new(.36));
            Check(d.Palette.Results.First().Place.Id == "action:window:save", "palette save command");
            d.Step(new(.38, Keys: [ImGuiKey.Enter]));
            d.Step(new(.40));
            Check(c.WindowPresets.Presets.Count == 5, "palette saves geometry");
            d.Context.ContextMenu.Actions = new ContextMenuFixture();
            var point = c.WindowPosition + c.WindowSize / 2;
            d.Step(new(.42, Mouse: point));
            d.Step(new(.44, Right: true));
            d.Step(new(.46, Right: false));
            var row = d.Context.ContextMenu.Rows["Window presets"];
            d.Step(new(.48, Mouse: row, Left: true));
            d.Step(new(.50, Left: false));
            d.Step(new(.52));
            Check(d.Context.ContextMenu.Rows.ContainsKey("Everyday"), "context submenu lists presets");
            row = d.Context.ContextMenu.Rows["Everyday"];
            d.Step(new(.54, Mouse: row, Left: true));
            d.Step(new(.56, Left: false));
            d.Step(new(.58));
            Check(!d.Context.ContextMenu.IsOpen && controller.LastApplied == 1, "context applies preset");
        }
        Console.WriteLine("Window preset frames: save / capacity, both styles, pre-Begin bounds, held-drag "
            + "deferral, palette apply / save and context submenu passed.");
    }
}