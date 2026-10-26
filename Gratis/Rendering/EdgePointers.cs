using System;
using System.Collections.Generic;
using System.Numerics;
using Gratis.Classes;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public readonly record struct EdgePointerTarget(Vector2 World, string Name, Vector4 Colour, SpotlightSource Source,
    uint? Icon = null);

public sealed class EdgePointers {
    private readonly List<EdgePointerTarget> targets = [];
    private readonly List<EdgePointerBounds> obstacles = [];
    public List<(EdgePointerTarget Target, Vector2 Center, float Distance)> Drawn { get; } = [];
    public bool InputActive { get; private set; }
    public bool PointerOver => MapDrawContext.Current.Config.IsGratis && MapDrawContext.Current.Config.EdgePointers
        && Drawn.Exists(p => Vector2.Abs(ImGui.GetMousePos() - p.Center) is var d
            && d.X <= 38 * MapDrawContext.Current.GlobalScale && d.Y <= 13 * MapDrawContext.Current.GlobalScale);
    private MapRendererFrame? renderer;
    public void Begin(MapRendererFrame frame) {
        renderer = frame;
        targets.Clear();
    }

    public void ClearPvp() {
        targets.Clear();
        Drawn.Clear();
        InputActive = false;
    }

    public void Observe(MarkerInfo marker) {
        if (MapDrawContext.Current.IsPvP && PvpMapPolicy.IsPlayer(marker))
            return;
        var config = MapDrawContext.Current.Config;
        if (!config.IsGratis || !config.EdgePointers || renderer == null)
            return;
        var quest = MarkerIcon.TryQuest(marker.IconId, out var q) && q.MainScenario
            && q.State is QuestMarkerState.InProgress or QuestMarkerState.Complete;
        var priority = marker.IsFlag || marker.IsTarget || marker.IsTracked || marker.IsObjective
            || marker.IsSpotlighted || quest;
        var party = config.EdgePointerParty
            && MarkerIcon.Family(marker) is MarkerIconFamily.Party or MarkerIconFamily.Alliance;
        if (!priority && !party)
            return;
        if (!priority && MapDrawContext.Current.Visibility.State(
            MarkerVisibility.Group(marker)) is MarkerVisibilityState.Quiet or MarkerVisibilityState.Hidden)
            return;
        var screen = ImGui.GetWindowPos() + marker.Position + marker.Offset;
        Add(new(renderer.ScreenToWorld(screen, ImGui.GetWindowPos()),
            marker.PrimaryText?.Invoke() ?? FlatMarkerDrawing.Label(marker),
            FlatMarkerDrawing.Color(FlatMarkerDrawing.Category(marker)),
            marker.IsFlag ? SpotlightSource.Flag : SpotlightSource.Quest, marker.IconId));
    }

    private void Add(EdgePointerTarget target) {
        if (!float.IsFinite(target.World.X) || !float.IsFinite(target.World.Y))
            return;
        if (!targets.Exists(t => Vector2.DistanceSquared(t.World, target.World) < 1))
            targets.Add(target);
    }

    public void Draw(IMapWindowHost host) {
        Drawn.Clear();
        InputActive = false;
        if (!host.Config.IsGratis || !host.Config.EdgePointers)
            return;
        var context = MapDrawContext.Current;
        var s = context.GlobalScale;
        if (host.Renderer.Spotlight is { Active: true, Request: { } request })
            Add(new(request.Target, "Spotlight target", FlatMarkerDrawing.Color(MarkerCategory.Objective),
                request.Source, request.Icon));
        if (host.Config.Pins && context.Assistance?.PinLayer is { } pins)
            foreach (var pin in pins.Store.ForMap(host.Renderer.ViewedMapId))
                if (pin.Pointer)
                    Add(new(pin.World, pin.Title, PinOverlay.Colours[pin.Colour], SpotlightSource.Pin));
        var origin = ImGui.GetWindowPos();
        var (min, max) = MapShape.Bounds(origin, ImGui.GetWindowSize());
        obstacles.Clear();
        var band = MathF.Min(100 * s, (max.Y - min.Y) * .28f);
        obstacles.Add(new(min, new(max.X, min.Y + band)));
        obstacles.Add(new(new(min.X, max.Y - band), max));
        targets.Sort((a, b) => Vector2.DistanceSquared(a.World,
            host.PlayerPosition ?? Vector2.Zero).CompareTo(Vector2.DistanceSquared(b.World,
                host.PlayerPosition ?? Vector2.Zero)));
        using var font = context.GetChromeFont(11 * s, ChromeFontRole.Chip)?.Push();
        foreach (var target in targets) {
            if (Drawn.Count >= Math.Clamp(host.Config.EdgePointerMax, 1, 12))
                break;
            var screen = host.Renderer.WorldToScreen(target.World, origin);
            var outside = EdgePointerMath.Outside(screen, min, max, host.Config.MapCircle);
            if (outside <= 0)
                continue;
            var distance = host.PlayerPosition is { } player ? Vector2.Distance(target.World, player) : float.NaN;
            var label = host.Config.EdgePointerDistance && float.IsFinite(distance) ? $"{distance:0}y" : "";
            var half = new Vector2(label.Length == 0 ? 13 * s : (ImGui.CalcTextSize(label).X + 34 * s) / 2, 12 * s);
            if (!EdgePointerMath.Project(screen, min, max, host.Config.MapCircle, half, 8 * s, obstacles,
                out var point))
                continue;
            obstacles.Add(new(point - half - new Vector2(4 * s), point + half + new Vector2(4 * s)));
            Drawn.Add((target, point, distance));
            var pulse = host.Config.ReduceMotion ? 1 : .87f + .13f * MathF.Sin((float)host.Now * MathF.Tau / 1.8f);
            var alpha = Math.Clamp(outside / (20 * s), 0, 1) * pulse;
            var list = ImGui.GetWindowDrawList();
            MapWindowDrawing.DrawSurface(list, point - half, point + half, GratisTheme.Radius.Control * s, surfaceOpacity: alpha);
            var tip = point - new Vector2(half.X - 12 * s, 0);
            var direction = Vector2.Normalize(screen - (min + max) / 2);
            var side = new Vector2(-direction.Y, direction.X);
            var colour = ImGui.GetColorU32(target.Colour with { W = alpha });
            list.AddLine(tip - direction * 4 * s + side * 5 * s, tip + direction * 3 * s, colour, 2 * s);
            list.AddLine(tip + direction * 3 * s, tip - direction * 4 * s - side * 5 * s, colour, 2 * s);
            if (label.Length > 0)
                TextDrawing.Draw(list, point + new Vector2(-half.X + 25 * s, -TextDrawing.Measure(label).Y / 2), colour,
                    label);
            if (context.WindowMoveInputBlocked || host.Palette?.IsOpen == true || !ImGui.IsWindowHovered())
                continue;
            if (!ImGui.IsMouseHoveringRect(point - half, point + half, false))
                continue;
            InputActive = true;
            UiMotion.TooltipText(target.Name);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                host.Renderer.BeginSpotlight(new(target.World, host.Renderer.ViewedMapId, target.Source,
                    Icon: target.Icon));
        }
    }

    public string Diagnostics() => $"edge-pointers enabled={MapDrawContext.Current.Config.EdgePointers} "
        + $"candidates={targets.Count} drawn={Drawn.Count} max={MapDrawContext.Current.Config.EdgePointerMax} "
        + $"priority-only={!MapDrawContext.Current.Config.EdgePointerParty}";
}