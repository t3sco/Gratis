using System.Numerics;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal sealed class ContextMenuFixture : IContextMenuActions {
    public int Flags, Captures, LayersUsed;
    public Vector2 Flag;
    public string MapName => "Middle La Noscea";
    public Vector2 Coordinates(Vector2 world) => CoordinateShare.WorldToMap(world, Vector2.Zero, 1);
    public string MapLink(Vector2 world) => CoordinateShare.Text(Coordinates(world), MapName);
    public void SetFlag(Vector2 world) {
        Flags++;
        Flag = world;
    }

    public bool HasFlag => Flags > Removed;
    public int Removed;
    public void RemoveFlag() => Removed = Flags;

    public void CaptureKeys() => Captures++;
    public IReadOnlyList<ContextMenuItem> RecentFlags =>
        [new("Recent rendezvous", GratisIcons.Flag, () => SetFlag(new(20, 40)))];
    public IReadOnlyList<ContextMenuItem> Layers => [new("Upper floor", GratisIcons.Layers, () => LayersUsed++)];
}

internal static class ContextMenuVerification {
    private static void Require(bool condition, string message) {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    public static void Nearest() {
        var config = PaletteVerification.Config();
        config.WindowSize = new(700, 600);
        config.TravelActions = true;
        using var driver = new ScriptedFrameDriver(config, new FrameScript { Width = 960, Height = 800 });
        var source = new TravelFixture(driver);
        var assistance = driver.Context.Assistance = new MapAssistance(NullTravelProvider.Instance, source, () => true,
            () => { });
        driver.Context.ContextMenu.Actions = new ContextMenuFixture();
        for (var i = 0; i < 5; i++)
            driver.Step(new(i * .02));
        var point = driver.Renderer.WorldToScreen(new(60, 0), driver.Window.MapDrawOffset);
        driver.Step(new(.12, Mouse: point));
        driver.Step(new(.14, Right: true));
        driver.Step(new(.16, Right: false));
        var row = driver.Context.ContextMenu.Rows["Show nearest aetheryte"];
        driver.Step(new(.18, Mouse: row, Left: true));
        driver.Step(new(.20, Left: false));
        driver.Step(new(.22));
        Require(driver.Renderer.Spotlight.Active && assistance.Chip?.Destination == TravelFixture.Hub,
            "explicit nearest action shows native chip even for near targets");
        Require(source.Teleports == 0, "showing transport never invokes teleport");
    }

    public static void Frames() {
        foreach (var circle in new[] { false, true }) {
            var config = PaletteVerification.Config();
            config.MapCircle = circle;
            config.WindowSize = new(680, 640);
            config.FollowPlayer = false;
            using var driver = new ScriptedFrameDriver(config, new FrameScript { Width = 960, Height = 800 });
            var ui = driver.Context.ContextMenu;
            var actions = new ContextMenuFixture();
            ui.Actions = actions;
            PinVerification.Attach(driver);
            double time = 0;
            FrameSnapshot Step(Vector2? mouse = null, bool? right = null, bool? left = null, ImGuiKey[]? keys = null,
                bool escape = false) =>

                driver.Step(new(time += .02, Mouse: mouse, Right: right, Left: left, Keys: keys, Escape: escape));
            for (var i = 0; i < 5; i++)
                Step();
            var point = config.WindowPosition + config.WindowSize / 2 + new Vector2(80, 30);
            Step(point);
            var before = driver.Renderer.DrawOffset;
            Step(right: true);
            Require(ui.IsOpen && ui.Rows.ContainsKey("Set flag here"), "right-click opens shared map menu");
            Require(!driver.Window.Dragging && before == driver.Renderer.DrawOffset, "right-click never pans");
            Step(right: false);
            Step(keys: [ImGuiKey.DownArrow]);
            Require(ui.Selected == 1, "Down selects next row");
            Step();
            Step(keys: [ImGuiKey.UpArrow]);
            Require(ui.Selected == 0, "Up selects previous row");
            Step();
            Step(keys: [ImGuiKey.Enter]);
            Require(actions.Flags == 1 && !ui.IsOpen && ui.LastAction == "Set flag here",
                "Enter invokes selected action and closes");
            Require(Vector2.Distance(actions.Flag,
                driver.Renderer.ScreenToWorld(point, driver.Window.MapDrawOffset)) < .01,
                "flag uses opening point");
            Step();
            Step(point, right: true);
            Step(right: false);
            Step(escape: true);
            Require(!ui.IsOpen && ui.CapturesInput && actions.Captures > 0, "Escape consumed on closing frame");
            Step();
            Step(point, right: true);
            Step(right: false);
            var recent = ui.Rows["Recent flags"];
            Step(recent, left: true);
            Step(left: false);
            Step();
            Require(ui.Rows.ContainsKey("Recent rendezvous"), "history submenu");
            var flag = ui.Rows["Recent rendezvous"];
            Step(flag, left: true);
            Step(left: false);
            Require(actions.Flags == 2 && actions.Flag == new Vector2(20, 40), "recent flag action");
            Step();
            Step(point, right: true);
            Step(right: false);
            foreach (var corner in new[] { ui.Min, new Vector2(ui.Max.X, ui.Min.Y), ui.Max,
                new Vector2(ui.Min.X, ui.Max.Y) })
                Require(corner.X >= config.WindowPosition.X && corner.Y >= config.WindowPosition.Y
                    && (!circle || Vector2.Distance(corner, config.WindowPosition + new Vector2(340, 320)) <= 320),
                    "menu corners clamped inside frame");
            Step(escape: true);
            Step();
            config.GratisContextMenu = false;
            Step(point, right: true);
            Require(!ui.IsOpen, "option off");
            config.VisualStyle = 1;
            Step(right: false);
            Step(right: true);
            Require(!ui.IsOpen, "Classic retains old menu");
        }
        var markerConfig = PaletteVerification.Config();
        markerConfig.WindowSize = new(680, 640);
        using var markers = new ScriptedFrameDriver(markerConfig, new FrameScript {
            Width = 960, Height = 800,
            Markers = [new(71203, Vector2.Zero)]
        });
        markers.Context.ContextMenu.Actions = new ContextMenuFixture();
        markers.Markers[0].PrimaryText = () => "Quest target";
        for (var i = 0; i < 5; i++)
            markers.Step(new(i * .02));
        var center = markers.Renderer.WorldToScreen(Vector2.Zero, markers.Window.MapDrawOffset);
        markers.Step(new(.12, Mouse: center));
        markers.Step(new(.14, Right: true));
        markers.Step(new(.16, Right: false));
        Require(markers.Context.ContextMenu.Marker != null && markers.Context.ContextMenu.Rows.ContainsKey("Copy name"),
            "marker-specific menu");
        var mute = markers.Context.ContextMenu.Rows["Mute this marker group"];
        markers.Step(new(.18, Mouse: mute, Left: true));
        markers.Step(new(.20, Left: false));
        markers.Step(new(.22));
        Require(markers.Context.ContextMenu.Rows.ContainsKey("Hide quests?"),
            "priority mute needs explicit confirmation");
        Require(markers.Context.Visibility.State(MarkerGroup.Quests, true) != MarkerVisibilityState.Hidden,
            "opening confirmation never hides priority");
        Console.WriteLine("Context menu: shared right-click/marker input, keyboard, actions, history, "
            + "confirmation, clamping, Classic and option gating.");
    }
}