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
    public static (Vector2 Min, Vector2 Max) SafeBounds() {
        var (min, max) = MapShape.Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
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
        var viewport = ImGui.GetMainViewport();
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
            foreach (var other in obstacles) {
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
    public static Vector4 Place(string id, Vector2 size, ChromeAnchor anchor) {
        Begin();
        var (min, max) = SafeBounds();
        var gap = GratisTheme.Gap * GratisTheme.ChromeScale;
        if (anchor <= ChromeAnchor.TopRight && handleBounds != Vector4.Zero)
            min.Y = MathF.Max(min.Y, MathF.Ceiling(handleBounds.W + gap));
        occupied.RemoveAll(p => p.Id == id);
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
                if (candidate.X >= min.X && candidate.Z <= max.X
                    && !occupied.Exists(p => Overlap(candidate, p.Bounds))) {
                    occupied.Add((id, candidate, anchor));
                    return candidate;
                }
                break;
            }
        }
        var result = Resolve(min, max, size, anchor, obstacles, gap);
        if (result != Vector4.Zero)
            occupied.Add((id, result, anchor));
        return result;
    }

    public static Vector2 Position(Vector4 bounds) => new(bounds.X, bounds.Y);
    public static Vector4 PlacePopover(string id, Vector2 size, Vector2 target) {
        Begin();
        occupied.RemoveAll(p => p.Id == id);
        obstacles.Clear();
        foreach (var item in occupied)
            obstacles.Add(item.Bounds);
        var (min, max) = SafeBounds();
        var result = TravelPopoverMath.Resolve(min, max, size, target, obstacles, 20 * GratisTheme.ChromeScale);
        if (result != Vector4.Zero)
            occupied.Add((id, result, ChromeAnchor.TopLeft));
        return result;
    }
}