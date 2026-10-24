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

public sealed partial class MeasureTool : IContextMenuMeasure
{
    public bool Active { get; private set; }
    public bool Dragging { get; private set; }
    public bool InputActive { get; private set; }
    public Vector2 Start { get; private set; }
    public Vector2 End { get; private set; }
    public uint MapId { get; private set; }
    public float Distance => Vector2.Distance(Start, End);
    public bool StartSnapped { get; private set; }
    public bool EndSnapped { get; private set; }
    public string LastCopy { get; private set; } = "";
    public Vector2 ChipMin { get; private set; }
    public Vector2 ChipMax { get; private set; }
    private readonly List<EdgePointerBounds> chrome = [];
    private bool awaitingEnd;
    public bool WantsGesture(MapDrawingConfig config) => config.IsGratis && config.MeasureTool &&
        (Dragging || awaitingEnd || !ImGui.GetIO().KeyAlt && ImGui.GetIO().KeyCtrl && ImGui.IsMouseDown(ImGuiMouseButton.Left));
    public void Begin(uint mapId, Vector2 world)
    {
        MapId = mapId; Start = End = world; Active = true; Dragging = false; awaitingEnd = true;
        StartSnapped = EndSnapped = false;
    }
    public void Clear() { Active = Dragging = awaitingEnd = false; }
    public void BeginFrame() { chrome.Clear(); InputActive = false; }
    public void Reserve(Vector2 min, Vector2 max) { if (max.X > min.X && max.Y > min.Y) chrome.Add(new(min, max)); }
    private void Reserve(Vector4 bounds) => Reserve(new(bounds.X, bounds.Y), new(bounds.Z, bounds.W));

    private Vector2 Snap(IMapWindowHost host, Vector2 screen, out bool snapped)
    {
        var origin = ImGui.GetWindowPos(); var s = MapDrawContext.Current.GlobalScale;
        var result = host.Renderer.ScreenToWorld(screen, origin); var best = 64 * s * s;
        snapped = false;
        foreach (var (marker, world) in MapDrawContext.Current.MarkerTargets.Targets) {
            if (MapDrawContext.Current.Visibility.State(MarkerVisibility.Group(marker), MarkerVisibility.IsPriority(marker)) == MarkerVisibilityState.Hidden) continue;
            var point = host.Renderer.WorldToScreen(world, origin);
            var square = Vector2.DistanceSquared(screen, point);
            if (square > best || !MapShape.Contains(point)) continue;
            best = square; result = world; snapped = true;
        }
        if (host.Config.Pins && MapDrawContext.Current.Assistance?.PinLayer is { } pins)
            foreach (var pin in pins.Store.ForMap(host.Renderer.ViewedMapId)) {
                if (MapDrawContext.Current.Visibility.State(MarkerGroup.Pins) == MarkerVisibilityState.Hidden) break;
                var point = host.Renderer.WorldToScreen(pin.World, origin);
                var square = Vector2.DistanceSquared(screen, point);
                if (square <= best && MapShape.Contains(point)) { best = square; result = pin.World; snapped = true; }
            }
        return result;
    }

    public void Draw(IMapWindowHost host, MoveHandleGeometry handle)
    {
        if (!host.Config.IsGratis || !host.Config.MeasureTool || Active && MapId != host.Renderer.ViewedMapId) { Clear(); return; }
        var context = MapDrawContext.Current; var mouse = ImGui.GetMousePos(); var s = context.GlobalScale;
        Reserve(MapWindowDrawing.TitleBounds); Reserve(MapWindowDrawing.CoordinateBounds);
        if (context.Chrome.ControlsAlpha(host.Config) >= .05f) Reserve(MapToolbarDrawing.LastBounds);
        Reserve(LensDrawing.LastBounds); if (host.Config.QuickFilterBar) Reserve(FilterBarDrawing.LastBounds);
        if (host.Config.MoveHandle) Reserve(handle.HitMin, handle.HitMax);
        var blocked = context.WindowMoveInputBlocked || context.Assistance?.InputActive == true || context.ContextMenu.CapturesInput || host.Palette?.CapturesInput == true;
        var focused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        if (Active && focused && !blocked && ImGui.IsKeyPressed(ImGuiKey.Escape, false)) { Clear(); InputActive = true; return; }
        var origin = ImGui.GetWindowPos();
        var a = host.Renderer.WorldToScreen(Start, origin); var b = host.Renderer.WorldToScreen(End, origin);
        var onRuler = Active && (MeasureToolMath.SegmentDistance(mouse, a, b) <= 7 * s || new EdgePointerBounds(ChipMin, ChipMax).Contains(mouse));
        var overMap = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem) && MapShape.Contains(mouse) && !chrome.Exists(r => r.Contains(mouse));
        var click = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        var began = false;
        if (!blocked && overMap && click && ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeyAlt) {
            Begin(host.Renderer.ViewedMapId, Snap(host, mouse, out var snapped));
            StartSnapped = snapped; Dragging = true; awaitingEnd = false; began = true;
        }
        else if (!blocked && Active && overMap && ImGui.IsMouseClicked(ImGuiMouseButton.Right) && onRuler) {
            LastCopy = FormattableString.Invariant($"{Distance:0.0}y"); ImGui.SetClipboardText(LastCopy);
            context.ContextMenu.Notify("Copied " + LastCopy); InputActive = true;
        }
        else if (!blocked && Active && click && overMap && !awaitingEnd) {
            if (!onRuler) Clear(); else InputActive = true;
        }
        if (Active && (Dragging || awaitingEnd) && !blocked) {
            End = Snap(host, mouse, out var snapped); EndSnapped = snapped; InputActive = true;
            if (Dragging && !ImGui.IsMouseDown(ImGuiMouseButton.Left) || awaitingEnd && click && !began && overMap) Dragging = awaitingEnd = false;
        }
        if (Active && Dragging && (!focused || blocked)) { Dragging = false; InputActive = true; }
        if (Active) DrawRuler(host);
    }
    public string Diagnostics() => $"measure enabled={MapDrawContext.Current.Config.MeasureTool} active={Active} dragging={Dragging} map={MapId} start={Start} end={End} distance={Distance:0.0}y snapped={StartSnapped}/{EndSnapped}";
}
