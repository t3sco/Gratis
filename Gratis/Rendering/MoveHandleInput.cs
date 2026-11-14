using System;
using System.Numerics;
using Gratis.Classes;
using Gratis.Data;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed class MoveHandleInput {
    private bool held, altGesture, shiftGesture, changed;
    private Vector2 startMouse, startPosition;
    private Vector2 contentInset, contentTrim;
    private (Vector2 Position, Vector2 Size)? restore;
    // The frame's grab zones: a band along each edge moves the window, a corner resizes it. See WindowEdgeZones.
    private WindowEdgeZone zone, near, dragZone, resizeZone, glowZone;
    private Vector2 resizeStartSize;
    private MoveWorkArea resizeArea;
    private float glow;
    private double lastTime = double.NaN;
    private float visibility;
    public bool Dragging { get; private set; }
    public bool BlocksMapInput { get; private set; }
    public bool Hovered { get; private set; }
    public float Visibility => visibility;
    public MoveHandleGeometry Geometry { get; private set; }
    /// <summary>The corner being dragged to resize the window.</summary>
    public bool Resizing => resizeZone != WindowEdgeZone.None;
    /// <summary>The edge band or corner under the pointer (or being dragged); None when the frame has no claim on it.</summary>
    public WindowEdgeZone Zone => Resizing ? resizeZone : Dragging && dragZone != WindowEdgeZone.None ? dragZone : zone;
    public static bool Enabled(SystemConfig config) => config.IsGratis ? config.MoveHandle : config.ClassicMoveHandle;
    /// <summary>Gratis with the move handle on moves from the edge bands and resizes from the corners; Classic is unchanged.</summary>
    public static bool EdgesEnabled(SystemConfig config) => config.IsGratis && config.MoveHandle && !config.LockWindow;
    /// <summary>Something else (a panel, the filter bar) owns the pointer this frame: the frame's zones step aside.</summary>
    public void YieldEdges() {
        if (!held)
            zone = near = WindowEdgeZone.None;
        glow = 0;
    }

    public void Cancel() {
        held = altGesture = shiftGesture = changed = Dragging = BlocksMapInput = Hovered = false;
        zone = near = dragZone = resizeZone = glowZone = WindowEdgeZone.None;
        glow = 0;
        visibility = 0;
        lastTime = double.NaN;
    }

    public void PrepareWindow(IMapWindowHost host) {
        if (!DisplayGeometry.ValidSize(ImGui.GetIO().DisplaySize) || !DisplayGeometry.ValidArea(host.MoveWorkArea))
            return;
        var config = host.Config;
        if (Resizing && !config.LockWindow) {
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) && !ImGui.IsMouseReleased(ImGuiMouseButton.Left)) {
                if (changed)
                    host.SaveWindowPosition();
                Cancel();
                return;
            }
            var (position, size) = WindowEdgeZones.Resize(resizeZone, startPosition, resizeStartSize,
                ImGui.GetMousePos() - startMouse, host.MinimumWindowSize, resizeArea, config.MapCircle);
            changed |= position != startPosition || size != resizeStartSize;
            config.WindowPosition = position;
            config.WindowSize = size;
            ImGui.SetNextWindowPos(position);
            ImGui.SetNextWindowSize(size);
            return;
        }
        if (!Dragging || config.LockWindow || altGesture && !ImGui.GetIO().KeyAlt
            || shiftGesture && !ImGui.GetIO().KeyShift)
            return;
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) && !ImGui.IsMouseReleased(ImGuiMouseButton.Left)) {
            if (changed)
                host.SaveWindowPosition();
            Cancel();
            return;
        }
        var scale = MapDrawContext.Current.GlobalScale;
        var target = startPosition + ImGui.GetMousePos() - startMouse;
        if (config.MoveHandleSnap && ImGui.GetIO().KeyShift)
            target = MoveHandleMath.SnapDrag(target, config.WindowSize, host.MoveWorkArea, scale);
        var (min, max) = MapShape.Bounds(contentInset, config.WindowSize - contentTrim);
        var local = MoveHandleMath.Geometry(min, max, config.MapCircle, scale);
        target = MoveHandleMath.Clamp(target, config.WindowSize, host.MoveWorkArea, local, scale);
        config.WindowPosition = target;
        changed |= target != startPosition;
        ImGui.SetNextWindowPos(target);
    }

    public void Update(IMapWindowHost host, bool mapDragging, bool yieldEdges = false) {
        if (!DisplayGeometry.ValidSize(ImGui.GetIO().DisplaySize) || !DisplayGeometry.ValidArea(host.MoveWorkArea))
            return;
        var config = host.Config;
        var context = MapDrawContext.Current;
        var scale = context.GlobalScale;
        var position = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        var content = ImGui.GetCursorScreenPos();
        var contentSize = ImGui.GetContentRegionAvail();
        contentInset = content - position;
        contentTrim = size - contentSize;
        var (min, max) = MapShape.Bounds(content, contentSize);
        Geometry = MoveHandleMath.Geometry(min, max, config.MapCircle, scale);
        if (!held && Enabled(config) && !config.LockWindow) {
            var area = host.MoveWorkAreaAt(Geometry.Center);
            var local = MoveHandleMath.Geometry(min - position, max - position, config.MapCircle, scale);
            var constrained = MoveHandleMath.Clamp(position, size, area, local, scale);
            if (constrained != position) {
                Apply(host, constrained, size, content - position, contentSize, scale, area);
                content += constrained - position;
                min += constrained - position;
                max += constrained - position;
                position = constrained;
                host.SaveWindowPosition();
            }
        }
        var mouse = ImGui.GetMousePos();
        var overWindow = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows
            | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        Hovered = Enabled(config) && !mapDragging && overWindow && Geometry.Contains(mouse);
        var inMap = mouse.X >= min.X && mouse.Y >= min.Y && mouse.X <= max.X && mouse.Y <= max.Y
            && (!config.MapCircle || Vector2.DistanceSquared(mouse,
                (min + max) / 2) <= MathF.Pow(MathF.Min(max.X - min.X, max.Y - min.Y) / 2, 2));
        var edges = EdgesEnabled(config);
        // Items under the pointer (toolbar buttons, scrollbars, pins) and anything the caller flags outrank the frame.
        // They are read from last frame, which is exactly when the pointer arrives over them.
        // The context menu (right button) and wheel zoom keep working over the edge too.
        var itemWantsPointer = ImGui.IsAnyItemHovered() || ImGui.IsAnyItemActive()
            || ImGui.IsMouseDown(ImGuiMouseButton.Right) || ImGui.GetIO().MouseWheel != 0;
        if (!held) {
            var (frameMin, frameMax) = MapShape.Bounds(position, size);
            zone = edges && overWindow && !mapDragging
                ? WindowEdgeZones.At(mouse, frameMin, frameMax, config.MapCircle, scale, yieldEdges || itemWantsPointer)
                : WindowEdgeZone.None;
            near = edges && overWindow && !mapDragging
                ? WindowEdgeZones.Near(mouse, frameMin, frameMax, config.MapCircle, scale, yieldEdges || itemWantsPointer)
                : WindowEdgeZone.None;
        }
        var bandHovered = WindowEdgeZones.IsMove(zone);
        var cornerHovered = WindowEdgeZones.IsCorner(zone);
        var alt = config.AltDragMovesWindow && (config.IsGratis || config.ClassicMoveHandle) && ImGui.GetIO().KeyAlt;
        var altReady = alt && overWindow && inMap;
        var legacyShift = config.EnableShiftDragMove && (config.IsGratis || config.ClassicMoveHandle)
            && ImGui.GetIO().KeyShift;
        // A button or chip under the pointer keeps its Shift-click (e.g. copying coordinates with the map name).
        var shiftReady = legacyShift && overWindow && inMap && (held || !itemWantsPointer);
        var grabbed = Hovered || bandHovered || cornerHovered;
        BlocksMapInput = held || grabbed || altReady || shiftReady;
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && (grabbed || altReady || shiftReady)) {
            held = true;
            altGesture = !grabbed && altReady;
            shiftGesture = !grabbed && !altReady;
            changed = false;
            startMouse = mouse;
            startPosition = position;
            dragZone = bandHovered ? zone : WindowEdgeZone.None;
            if (!config.LockWindow && cornerHovered) {
                ImGui.SetWindowFocus();
                resizeZone = zone;
                resizeStartSize = size;
                resizeArea = host.MoveWorkAreaAt(position + size / 2);
            }
            else if (!config.LockWindow) {
                ImGui.SetWindowFocus();
                if (Hovered && config.MoveHandleSnap && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) {
                    var target = restore;
                    if (target.HasValue) {
                        position = target.Value.Position;
                        size = target.Value.Size;
                        restore = null;
                    }
                    else {
                        restore = (position, size);
                        position = MoveHandleMath.NearestEdge(position, size, host.MoveWorkArea, scale);
                    }
                    Apply(host, position, size, content - ImGui.GetWindowPos(), contentSize, scale);
                    host.SaveWindowPosition();
                }
                else
                    Dragging = true;
            }
        }
        var modifierReleased = altGesture && !alt || shiftGesture && !legacyShift;
        if (Dragging && !modifierReleased && !config.LockWindow) {
            var target = startPosition + mouse - startMouse;
            if (config.MoveHandleSnap && ImGui.GetIO().KeyShift)
                target = MoveHandleMath.SnapDrag(target, size, host.MoveWorkArea, scale);
            Apply(host, target, size, content - ImGui.GetWindowPos(), contentSize, scale);
            changed |= target != startPosition;
        }
        if (Dragging && (!ImGui.IsMouseDown(ImGuiMouseButton.Left) || modifierReleased || config.LockWindow)) {
            Dragging = false;
            if (changed)
                host.SaveWindowPosition();
        }
        if (Resizing && (!ImGui.IsMouseDown(ImGuiMouseButton.Left) || !edges)) {
            resizeZone = WindowEdgeZone.None;
            if (changed)
                host.SaveWindowPosition();
        }
        // Keep ownership through release (also after releasing Alt), never handing a press to pan.
        BlocksMapInput |= held;
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) {
            held = false;
            dragZone = WindowEdgeZone.None;
        }
        var shown = Zone;
        if (WindowEdgeZones.IsCorner(shown))
            ImGui.SetMouseCursor(shown is WindowEdgeZone.CornerNW or WindowEdgeZone.CornerSE
                ? DiagonalCursor.NorthWestSouthEast : DiagonalCursor.NorthEastSouthWest);
        else if ((Hovered || bandHovered || altReady || shiftReady || Dragging) && !config.LockWindow)
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
        // The cue fades in while the pointer is merely near an edge or corner, and brightens once it can grab.
        var cue = shown != WindowEdgeZone.None ? shown : near;
        if (cue != WindowEdgeZone.None)
            glowZone = cue;
        glow = config.IsGratis
            ? UiMotion.Value(ImGui.GetID("##edge-zone-glow"), 13, shown != WindowEdgeZone.None ? 1 : cue != WindowEdgeZone.None ? .55f : 0,
                InterfaceMotionTokens.Micro, initial: 0, opacity: true)
            : 0;
        if (Hovered && config.LockWindow)
            UiMotion.TooltipText("Window position is locked. Unlock it in Advanced settings to move the map.");
        var topHover = overWindow && mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y
            && mouse.Y <= min.Y + 16 * scale;
        var targetVisibility = Enabled(config)
            && (topHover || Hovered || Dragging || context.Chrome.ControlsAlpha(config) > .05f);
        var dt = double.IsFinite(lastTime) ? Math.Max(0, host.Now - lastTime) : 0;
        lastTime = host.Now;
        visibility = config.IsGratis
            ? UiMotion.Value(ImGui.GetID("##move-handle-reveal"), 12, targetVisibility ? 1 : 0,
                InterfaceMotionTokens.Micro, initial: 0, opacity: true)
            : Math.Clamp(visibility + (float)(dt / .12) * (targetVisibility ? 1 : -1), 0, 1);
    }

    private void Apply(IMapWindowHost host, Vector2 position, Vector2 size, Vector2 contentOffset, Vector2 contentSize,
        float scale, MoveWorkArea? area = null) {
        var (localMin, localMax) = MapShape.Bounds(contentOffset, size - (ImGui.GetWindowSize() - contentSize));
        var localHandle = MoveHandleMath.Geometry(localMin, localMax, host.Config.MapCircle, scale);
        position = MoveHandleMath.Clamp(position, size, area ?? host.MoveWorkArea, localHandle, scale);
        ImGui.SetWindowSize(size);
        ImGui.SetWindowPos(position);
        host.Config.WindowPosition = position;
        host.Config.WindowSize = size;
        Geometry = MoveHandleMath.Geometry(localMin + position, localMax + position, host.Config.MapCircle, scale);
    }

    /// <summary>
    /// A quiet brightening of the band or corner under the pointer; the accent colour once it is being dragged. A faint
    /// dark underlay keeps it readable on pale maps. The fade follows the interface animation setting (instant when
    /// animations are off).
    /// </summary>
    private void DrawZone(SystemConfig config) {
        if (glow <= .004f || glowZone == WindowEdgeZone.None || !EdgesEnabled(config))
            return;
        var scale = MapDrawContext.Current.GlobalScale;
        var (min, max) = MapShape.Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        var (corner, band) = WindowEdgeZones.Metrics(max - min, scale);
        if (WindowEdgeZones.IsMove(glowZone)) {
            DrawEdgePill(config, min, max, scale);
            return;
        }
        var active = Resizing || Dragging && WindowEdgeZones.IsMove(dragZone);
        var ink = active ? config.AccentColor : Vector4.One;
        ink.W = (active ? .9f : .6f) * glow;
        var thickness = MathF.Max(1, MathF.Min(3 * scale, band));
        var list = ImGui.GetWindowDrawList();
        var shadow = ImGui.GetColorU32(new Vector4(0, 0, 0, .28f * glow));
        DrawZoneShape(list, config, min, max, corner, thickness + 2 * MathF.Max(1, scale), shadow, thickness);
        DrawZoneShape(list, config, min, max, corner, thickness, ImGui.GetColorU32(ink), thickness);
    }

    // The zone as a stroke of the given width, centred where a stroke of baseThickness would sit just inside the edge.
    private void DrawZoneShape(ImDrawListPtr list, SystemConfig config, Vector2 min, Vector2 max, float corner,
        float thickness, uint color, float baseThickness) {
        var scale = MapDrawContext.Current.GlobalScale;
        var centreLine = MathF.Max(1, scale) + baseThickness / 2 + MathF.Max(1, scale);
        if (config.MapCircle) {
            var center = (min + max) / 2;
            var radius = MathF.Min(max.X - min.X, max.Y - min.Y) / 2 - centreLine;
            if (radius <= 0)
                return;
            // Corners sit on the diagonals.
            var gap = MathF.Min(MathF.PI / 4, corner / radius);
            var middle = glowZone switch {
                WindowEdgeZone.CornerSE => MathF.PI / 4,
                WindowEdgeZone.CornerSW => MathF.PI * .75f,
                WindowEdgeZone.CornerNW => MathF.PI * 1.25f,
                _ => MathF.PI * 1.75f,
            };
            var half = gap * 1.25f;
            if (half <= 0)
                return;
            list.PathArcTo(center, radius, middle - half, middle + half, FrameMaterialDrawing.ArcSegments(radius, half * 2));
            list.PathStroke(color, ImDrawFlags.None, thickness);
            return;
        }
        var rounding = MapShape.Rounding(max - min);
        rounding = MathF.Max(rounding, thickness * 2);
        var arm = corner * 1.8f;
        var west = glowZone is WindowEdgeZone.CornerNW or WindowEdgeZone.CornerSW;
        var north = glowZone is WindowEdgeZone.CornerNW or WindowEdgeZone.CornerNE;
        var arcCenter = new Vector2(west ? min.X + rounding : max.X - rounding, north ? min.Y + rounding : max.Y - rounding);
        var arcRadius = MathF.Max(1, rounding - centreLine);
        if (rounding * MathF.PI / 2 >= arm) {
            // A rounded corner longer than the grab zone: stroke the part of the arc that matches it.
            var angle = glowZone switch {
                WindowEdgeZone.CornerNW => MathF.PI * 1.25f,
                WindowEdgeZone.CornerNE => MathF.PI * 1.75f,
                WindowEdgeZone.CornerSE => MathF.PI / 4,
                _ => MathF.PI * .75f,
            };
            var sweep = MathF.Min(MathF.PI / 4, arm / (2 * rounding));
            list.PathArcTo(arcCenter, arcRadius, angle - sweep, angle + sweep, FrameMaterialDrawing.ArcSegments(arcRadius, sweep * 2));
            list.PathStroke(color, ImDrawFlags.None, thickness);
            return;
        }
        // A tight corner: the whole quarter arc, with a straight arm along each edge to fill out the grab zone.
        var length = (arm - rounding * MathF.PI / 2) / 2;
        var inward = new Vector2(west ? 1 : -1, north ? 1 : -1);
        var horizontalEdge = north ? min.Y + centreLine : max.Y - centreLine;
        var verticalEdge = west ? min.X + centreLine : max.X - centreLine;
        list.PathLineTo(new(arcCenter.X + inward.X * length, horizontalEdge));
        list.PathArcTo(arcCenter, arcRadius, north ? MathF.PI * 1.5f : MathF.PI / 2, west ? MathF.PI : north ? MathF.PI * 2 : 0,
            FrameMaterialDrawing.ArcSegments(arcRadius, MathF.PI / 2));
        list.PathLineTo(new(verticalEdge, arcCenter.Y + inward.Y * length));
        list.PathStroke(color, ImDrawFlags.None, thickness);
    }

    // An edge cue is a small pill like the move handle at the top, not a strip along the whole edge. The top edge already
    // shows that handle, so it is left to it.
    private void DrawEdgePill(SystemConfig config, Vector2 min, Vector2 max, float scale) {
        if (glowZone == WindowEdgeZone.MoveNorth)
            return;
        var horizontal = glowZone == WindowEdgeZone.MoveSouth;
        var half = (horizontal ? new Vector2(18, 2) : new Vector2(2, 18)) * scale;
        var inset = 8 * scale;
        Vector2 centre;
        if (config.MapCircle) {
            var middle = (min + max) / 2;
            var radius = MathF.Min(max.X - min.X, max.Y - min.Y) / 2;
            var depth = radius - MathF.Sqrt(MathF.Max(0, radius * radius - 28 * scale * 28 * scale)) + inset;
            var direction = glowZone switch {
                WindowEdgeZone.MoveEast => new Vector2(1, 0),
                WindowEdgeZone.MoveSouth => new Vector2(0, 1),
                _ => new Vector2(-1, 0),
            };
            centre = middle + direction * (radius - depth);
        }
        else
            centre = glowZone switch {
                WindowEdgeZone.MoveSouth => new Vector2((min.X + max.X) / 2, max.Y - inset),
                WindowEdgeZone.MoveEast => new Vector2(max.X - inset, (min.Y + max.Y) / 2),
                _ => new Vector2(min.X + inset, (min.Y + max.Y) / 2),
            };
        var active = Dragging && WindowEdgeZones.IsMove(dragZone);
        var ink = active ? config.AccentColor : Vector4.One;
        ink.W = (active ? .9f : .7f) * glow;
        var list = ImGui.GetWindowDrawList();
        var grow = new Vector2(MathF.Max(1, scale));
        list.AddRectFilled(centre - half - grow, centre + half + grow, ImGui.GetColorU32(new Vector4(0, 0, 0, .28f * glow)),
            GratisTheme.Radius.Pill(2 * (half + grow)));
        list.AddRectFilled(centre - half, centre + half, ImGui.GetColorU32(ink), GratisTheme.Radius.Pill(2 * half));
    }

    public void Draw(SystemConfig config) {
        DrawZone(config);
        if (!Enabled(config) || visibility <= 0)
            return;
        var scale = MapDrawContext.Current.GlobalScale;
        var color = Dragging ? config.AccentColor : Vector4.One;
        color.W = (Dragging ? .85f : config.LockWindow ? .2f : .4f) * visibility;
        var half = new Vector2(18, 2) * scale;
        ImGui.GetWindowDrawList().AddRectFilled(Geometry.Center - half, Geometry.Center + half,
            ImGui.GetColorU32(color), config.IsGratis ? GratisTheme.Radius.Pill(2 * half) : 2 * scale);
    }

    public readonly struct InputScope : IDisposable {
        public InputScope(bool blocked) {
            ImGui.PushStyleVar(ImGuiStyleVar.DisabledAlpha, 1);
            ImGui.BeginDisabled(blocked);
        }
        public void Dispose() {
            ImGui.EndDisabled();
            ImGui.PopStyleVar();
        }
    }
}

// The Dalamud bindings spell these two differently from ImGuiNET.
internal static class DiagonalCursor {
#if GRATIS_PREVIEW
    public const ImGuiMouseCursor NorthWestSouthEast = ImGuiMouseCursor.ResizeNWSE;
    public const ImGuiMouseCursor NorthEastSouthWest = ImGuiMouseCursor.ResizeNESW;
#else
    public const ImGuiMouseCursor NorthWestSouthEast = ImGuiMouseCursor.ResizeNwse;
    public const ImGuiMouseCursor NorthEastSouthWest = ImGuiMouseCursor.ResizeNesw;
#endif
}

public static class MoveHandleScreen {
    public static MoveWorkArea At(Vector2 point) {
        var viewport = ImGui.GetMainViewport();
        var main = new MoveWorkArea(viewport.WorkPos, viewport.WorkPos + viewport.WorkSize);
        if ((ImGui.GetIO().ConfigFlags & ImGuiConfigFlags.ViewportsEnable) == 0)
            return main;
        var monitors = ImGui.GetPlatformIO().Monitors;
        var nearest = float.PositiveInfinity;
        MoveWorkArea result = default;
        for (var index = 0; index < monitors.Size; index++) {
            var monitor = monitors[index];
            var area = new MoveWorkArea(monitor.WorkPos, monitor.WorkPos + monitor.WorkSize);
            if (!DisplayGeometry.ValidArea(area))
                continue;
            var distance = Vector2.DistanceSquared(point, Vector2.Clamp(point, area.Min, area.Max));
            if (distance >= nearest)
                continue;
            nearest = distance;
            result = area;
        }
        if (float.IsFinite(nearest))
            return result;
        return main;
    }
}
