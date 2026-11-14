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

public enum ChromeAnchor {
    TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight
}

public static class ChromeLayout {
    private static readonly List<(string Id, Vector4 Bounds, ChromeAnchor Anchor)> occupied = [];
    private static int frame = -1;
    private static MapDrawContext? context;
    private static Vector4 handleBounds;
    public static (Vector2 Min, Vector2 Max) SafeBounds() =>
        RawSafeBounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());

    public static (Vector2 Min, Vector2 Max) ToolbarSafeBounds() {
        var (min, max) = RawSafeBounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        var buttons = CornerButtons(MapDrawContext.Current, max.X - min.X);
        if (buttons > 0)
            max.X = MathF.Max(min.X, max.X - buttons * (GratisTheme.ToolbarHeight + GratisTheme.Gap) * GratisTheme.ChromeScale);
        return (min, max);
    }

    // The top-right corner holds the quest log's toggle and pin, with the FATE button to their left. The FATE button
    // stays in PvP (disabled, with its reason), where the quest log is hidden altogether.
    public static bool QuestButtonsShown(MapDrawContext context) =>
        context.Config.IsGratis && context.QuestLogSource != null && !context.IsPvP;

    public static bool FateButtonShown(MapDrawContext context) => context.Config.IsGratis && context.FateSource != null;

    // The Shared FATE button is the last in the row, to the left of the FATE button.
    public static bool SharedFateButtonShown(MapDrawContext context) =>
        context.Config.IsGratis && context.SharedFateSource != null;

    // The toolbar keeps priority: this optional button is dropped when adding it would leave the toolbar less than its
    // smallest form (a very small or circular map), so those windows lay out exactly as they did without it.
    public static bool SharedFateButtonFits(MapDrawContext context, float safeWidth) {
        var scale = GratisTheme.ChromeScale;
        var buttons = (QuestButtonsShown(context) ? 2 : 0) + (FateButtonShown(context) ? 1 : 0) + 1;
        return safeWidth - buttons * (GratisTheme.ToolbarHeight + GratisTheme.Gap) * scale
            >= MapToolbarDrawing.CollapsedWidth(2) * scale;
    }

    public static int CornerButtons(MapDrawContext context, float safeWidth = float.PositiveInfinity) =>
        (QuestButtonsShown(context) ? 2 : 0) + (FateButtonShown(context) ? 1 : 0)
        + (SharedFateButtonShown(context) && SharedFateButtonFits(context, safeWidth) ? 1 : 0);

    public static (Vector2 Min, Vector2 Max) RawSafeBounds(Vector2 position, Vector2 size) {
        var (min, max) = MapShape.Bounds(position, size);
        if (MapDrawContext.Current.Config.MapCircle) {
            var inset = (max - min) * .14644661f;
            min += inset;
            max -= inset;
        }
        var insetSize = GratisTheme.Inset * GratisTheme.ChromeScale;
        if (!MapDrawContext.Current.Config.MapCircle)
            insetSize += MapShape.Rounding(max - min) * .29289322f;
        var padding = new Vector2(MathF.Ceiling(insetSize));
        return (min + padding, Vector2.Max(min + padding, max - padding));
    }

    public static (Vector2 Min, Vector2 Max) PopoverBounds() {
        var viewport = ImGui.GetWindowViewport();
        var margin = new Vector2(MathF.Ceiling(GratisTheme.Inset * GratisTheme.ChromeScale));
        return (viewport.WorkPos + margin, Vector2.Max(viewport.WorkPos + margin, viewport.WorkPos + viewport.WorkSize - margin));
    }

    private static void Begin() {
        if (frame == ImGui.GetFrameCount() && ReferenceEquals(context, MapDrawContext.Current))
            return;
        frame = ImGui.GetFrameCount();
        context = MapDrawContext.Current;
        occupied.Clear();
        handleBounds = Vector4.Zero;
        if (context.Config.IsGratis && context.Config is Gratis.Data.SystemConfig { MoveHandle: true }) {
            var (min, max) = MapShape.Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
            var handle = MoveHandleMath.Geometry(min, max, context.Config.MapCircle, context.GlobalScale);
            handleBounds = new(handle.HitMin.X, handle.HitMin.Y, handle.HitMax.X, handle.HitMax.Y);
            occupied.Add(("move-handle", handleBounds, ChromeAnchor.TopCenter));
        }
    }

    public static bool Overlap(Vector4 a, Vector4 b) => a.Z > a.X && a.W > a.Y && b.Z > b.X && b.W > b.Y && a.X < b.Z
        && a.Z > b.X && a.Y < b.W && a.W > b.Y;
    public static Vector4 Resolve(Vector2 min, Vector2 max, Vector2 size, ChromeAnchor anchor,
        IReadOnlyList<Vector4> obstacles, float gap) {
        if (size.X <= 0 || size.Y <= 0 || size.X > max.X - min.X + .01f || size.Y > max.Y - min.Y + .01f)
            return Vector4.Zero;
        if (!Enum.IsDefined(anchor))
            anchor = ChromeAnchor.TopLeft;
        var bottom = anchor >= ChromeAnchor.BottomLeft;
        var column = (int)anchor % 3;
        var x = column == 0 ? min.X : column == 1 ? (min.X + max.X - size.X) / 2 : max.X - size.X;
        var y = bottom ? max.Y - size.Y : min.Y;
        for (var attempt = 0; attempt <= obstacles.Count; attempt++) {
            var bounds = new Vector4(x, y, x + size.X, y + size.Y);
            var hit = false;
            // Indexed rather than foreach: enumerating through the interface boxes the list enumerator every call.
            for (var index = 0; index < obstacles.Count; index++) {
                var other = obstacles[index];
                var expanded = other + new Vector4(-gap, -gap, gap, gap);
                if (!Overlap(bounds, expanded))
                    continue;
                y = bottom ? other.Y - gap - size.Y : other.W + gap;
                hit = true;
                break;
            }
            if (!hit)
                return y >= min.Y - .01f && y + size.Y <= max.Y + .01f ? bounds : Vector4.Zero;
        }
        return Vector4.Zero;
    }
    private static readonly List<Vector4> obstacles = [];

    // The three helpers below replace List.RemoveAll / Exists with capturing lambdas, which allocated a closure and a
    // delegate on every call (several calls per frame). RemoveOccupied keeps the order of the remaining entries.
    private static void RemoveOccupied(string id) {
        var write = 0;
        for (var read = 0; read < occupied.Count; read++) {
            var item = occupied[read];
            if (item.Id == id)
                continue;
            occupied[write++] = item;
        }
        if (write < occupied.Count)
            occupied.RemoveRange(write, occupied.Count - write);
    }

    private static bool OccupiedOverlaps(Vector4 candidate) {
        for (var index = 0; index < occupied.Count; index++)
            if (Overlap(candidate, occupied[index].Bounds))
                return true;
        return false;
    }

    private static bool ObstaclesOverlap(Vector4 candidate, float gap) {
        for (var index = 0; index < obstacles.Count; index++)
            if (Overlap(candidate, obstacles[index] + new Vector4(-gap, -gap, gap, gap)))
                return true;
        return false;
    }

    public static Vector4 Place(string id, Vector2 size, ChromeAnchor anchor) {
        Begin();
        var (min, max) = id == "toolbar" ? ToolbarSafeBounds() : SafeBounds();
        var gap = GratisTheme.Gap * GratisTheme.ChromeScale;
        if (anchor <= ChromeAnchor.TopRight && handleBounds != Vector4.Zero)
            min.Y = MathF.Max(min.Y, MathF.Ceiling(handleBounds.W + gap));
        RemoveOccupied(id);
        obstacles.Clear();
        foreach (var item in occupied)
            obstacles.Add(item.Bounds);
        if (id == "location") {
            foreach (var item in occupied) {
                if (item.Id != "coordinates" || item.Anchor != anchor)
                    continue;
                var right = (int)anchor % 3 == 2;
                var x = right ? item.Bounds.X - gap - size.X : item.Bounds.Z + gap;
                var candidate = new Vector4(x, item.Bounds.Y, x + size.X, item.Bounds.Y + size.Y);
                if (candidate.X >= min.X && candidate.Z <= max.X && candidate.W <= max.Y
                    && !OccupiedOverlaps(candidate)) {
                    occupied.Add((id, candidate, anchor));
                    return candidate;
                }
                break;
            }
        }
        var result = Resolve(min, max, size, anchor, obstacles, gap);
        if (id == "toolbar" && anchor == ChromeAnchor.TopCenter && result != Vector4.Zero) {
            var (fullMin, fullMax) = RawSafeBounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
            var x = (fullMin.X + fullMax.X - size.X) / 2;
            if (x >= fullMin.X && x + size.X <= max.X) {
                var centred = new Vector4(x, result.Y, x + size.X, result.W);
                if (!ObstaclesOverlap(centred, gap))
                    result = centred;
            }
        }
        if (result != Vector4.Zero)
            occupied.Add((id, result, anchor));
        return result;
    }

    public static void Release(string id) {
        Begin();
        RemoveOccupied(id);
    }

    public static Vector2 Position(Vector4 bounds) => new(bounds.X, bounds.Y);
    public static Vector4 PlacePopover(string id, Vector2 size, Vector2 target, bool allowOverlap = false) {
        Begin();
        RemoveOccupied(id);
        obstacles.Clear();
        foreach (var item in occupied)
            obstacles.Add(item.Bounds);
        var (min, max) = SafeBounds();
        var result = TravelPopoverMath.Resolve(min, max, size, target, obstacles, 20 * GratisTheme.ChromeScale, allowOverlap);
        if (result != Vector4.Zero)
            occupied.Add((id, result, ChromeAnchor.TopLeft));
        return result;
    }
}
