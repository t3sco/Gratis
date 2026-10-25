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

public sealed class MoveHandleInput
{
    private bool held, altGesture, shiftGesture, changed;
    private Vector2 startMouse, startPosition;
    private Vector2 contentInset, contentTrim;
    private (Vector2 Position, Vector2 Size)? restore;
    private double lastTime = double.NaN;
    private float visibility;
    public bool Dragging { get; private set; }
    public bool BlocksMapInput { get; private set; }
    public bool Hovered { get; private set; }
    public float Visibility => visibility;
    public MoveHandleGeometry Geometry { get; private set; }
    public static bool Enabled(SystemConfig config) => config.IsGratis ? config.MoveHandle : config.ClassicMoveHandle;
    public void Cancel()
    {
        held = altGesture = shiftGesture = changed = Dragging = BlocksMapInput = Hovered = false;
        visibility = 0; lastTime = double.NaN;
    }

    public void PrepareWindow(IMapWindowHost host)
    {
        var config = host.Config;
        if (!Dragging || config.LockWindow || altGesture && !ImGui.GetIO().KeyAlt || shiftGesture && !ImGui.GetIO().KeyShift) return;
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) && !ImGui.IsMouseReleased(ImGuiMouseButton.Left)) {
            if (changed) host.SaveWindowPosition();
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

    public void Update(IMapWindowHost host, bool mapDragging)
    {
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
                content += constrained - position; min += constrained - position; max += constrained - position;
                position = constrained;
                host.SaveWindowPosition();
            }
        }
        var mouse = ImGui.GetMousePos();
        var overWindow = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        Hovered = Enabled(config) && !mapDragging && overWindow && Geometry.Contains(mouse);
        var inMap = mouse.X >= min.X && mouse.Y >= min.Y && mouse.X <= max.X && mouse.Y <= max.Y &&
            (!config.MapCircle || Vector2.DistanceSquared(mouse, (min + max) / 2) <= MathF.Pow(MathF.Min(max.X - min.X, max.Y - min.Y) / 2, 2));
        var alt = config.AltDragMovesWindow && (config.IsGratis || config.ClassicMoveHandle) && ImGui.GetIO().KeyAlt;
        var altReady = alt && overWindow && inMap;
        var legacyShift = config.EnableShiftDragMove && (config.IsGratis || config.ClassicMoveHandle) && ImGui.GetIO().KeyShift;
        var shiftReady = legacyShift && overWindow && inMap;
        BlocksMapInput = held || Hovered || altReady || shiftReady;
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && (Hovered || altReady || shiftReady)) {
            held = true; altGesture = !Hovered && altReady; shiftGesture = !Hovered && !altReady; changed = false;
            startMouse = mouse; startPosition = position;
            if (!config.LockWindow) {
                ImGui.SetWindowFocus();
                if (Hovered && config.MoveHandleSnap && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) {
                    var target = restore;
                    if (target.HasValue) {
                        position = target.Value.Position; size = target.Value.Size; restore = null;
                    }
                    else {
                        restore = (position, size);
                        position = MoveHandleMath.NearestEdge(position, size, host.MoveWorkArea, scale);
                    }
                    Apply(host, position, size, content - ImGui.GetWindowPos(), contentSize, scale);
                    host.SaveWindowPosition();
                }
                else Dragging = true;
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
            if (changed) host.SaveWindowPosition();
        }
        BlocksMapInput |= held;
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) held = false;
        if ((Hovered || altReady || shiftReady || Dragging) && !config.LockWindow) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
        if (Hovered && config.LockWindow) UiMotion.TooltipText("Window position is locked. Unlock it in Advanced settings to move the map.");
        var topHover = overWindow && mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= min.Y + 16 * scale;
        var targetVisibility = Enabled(config) && (topHover || Hovered || Dragging || context.Chrome.ControlsAlpha(config) > .05f);
        var dt = double.IsFinite(lastTime) ? Math.Max(0, host.Now - lastTime) : 0;
        lastTime = host.Now;
        visibility = Math.Clamp(visibility + (float)(dt / .12) * (targetVisibility ? 1 : -1), 0, 1);
    }

    private void Apply(IMapWindowHost host, Vector2 position, Vector2 size, Vector2 contentOffset, Vector2 contentSize, float scale, MoveWorkArea? area = null)
    {
        var (localMin, localMax) = MapShape.Bounds(contentOffset, size - (ImGui.GetWindowSize() - contentSize));
        var localHandle = MoveHandleMath.Geometry(localMin, localMax, host.Config.MapCircle, scale);
        position = MoveHandleMath.Clamp(position, size, area ?? host.MoveWorkArea, localHandle, scale);
        ImGui.SetWindowSize(size);
        ImGui.SetWindowPos(position);
        host.Config.WindowPosition = position;
        host.Config.WindowSize = size;
        Geometry = MoveHandleMath.Geometry(localMin + position, localMax + position, host.Config.MapCircle, scale);
    }

    public void Draw(SystemConfig config)
    {
        if (!Enabled(config) || visibility <= 0) return;
        var scale = MapDrawContext.Current.GlobalScale;
        var color = Dragging ? config.AccentColor : Vector4.One;
        color.W = (Dragging ? .85f : config.LockWindow ? .2f : .4f) * visibility;
        var half = new Vector2(18, 2) * scale;
        ImGui.GetWindowDrawList().AddRectFilled(Geometry.Center - half, Geometry.Center + half,
            ImGui.GetColorU32(color), 2 * scale);
    }

    public readonly struct InputScope : IDisposable
    {
        public InputScope(bool blocked)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.DisabledAlpha, 1);
            ImGui.BeginDisabled(blocked);
        }
        public void Dispose() { ImGui.EndDisabled(); ImGui.PopStyleVar(); }
    }
}

public static class MoveHandleScreen
{
    public static MoveWorkArea At(Vector2 point)
    {
        var monitors = ImGui.GetPlatformIO().Monitors;
        var nearest = float.PositiveInfinity;
        MoveWorkArea result = default;
        for (var index = 0; index < monitors.Size; index++) {
            var monitor = monitors[index];
            var area = new MoveWorkArea(monitor.WorkPos, monitor.WorkPos + monitor.WorkSize);
            var distance = Vector2.DistanceSquared(point, Vector2.Clamp(point, area.Min, area.Max));
            if (distance >= nearest) continue;
            nearest = distance; result = area;
        }
        if (float.IsFinite(nearest)) return result;
        var viewport = ImGui.GetMainViewport();
        return new(viewport.WorkPos, viewport.WorkPos + viewport.WorkSize);
    }
}
