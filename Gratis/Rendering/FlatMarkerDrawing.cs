using System.Numerics;
using Gratis.Classes;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public enum MarkerCategory { Auto, Poi, Transport, Objective, Link, Service, Party }

public static class FlatMarkerDrawing
{
    public static MarkerCategory Category(MarkerInfo marker)
    {
        if (marker.Category != MarkerCategory.Auto) return marker.Category;
        if (marker.MarkerType is MarkerType.Fate or MarkerType.Stellar) return MarkerCategory.Objective;
        return marker.IconId switch {
            60430 or 60453 or 60454 => MarkerCategory.Transport,
            60403 or 60421 or 60444 or 60961 => MarkerCategory.Party,
            60401 or 60402 or 60422 or 60424 or 60071 => MarkerCategory.Objective,
            >= 60501 and <= 60520 => MarkerCategory.Objective,
            >= 71000 and < 76000 => MarkerCategory.Objective,
            >= 60004 and <= 60039 or 60091 or 61731 => MarkerCategory.Service,
            _ when DrawHelpers.IsRegionIcon(marker.IconId) => MarkerCategory.Link,
            _ => MarkerCategory.Poi,
        };
    }

    public static string Label(MarkerCategory category) => category switch {
        MarkerCategory.Transport => "Aetheryte / transport",
        MarkerCategory.Objective => "Quest / objective",
        MarkerCategory.Link => "Map / instance link",
        MarkerCategory.Service => "Shop / service",
        MarkerCategory.Party => "Party / player",
        _ => "Point of interest",
    };

    public static Vector4 Color(MarkerCategory category)
    {
        var config = MapDrawContext.Current.Config;
        var color = category switch {
            MarkerCategory.Transport => config.TransportMarkerColor,
            MarkerCategory.Objective => config.ObjectiveMarkerColor,
            MarkerCategory.Link => config.LinkMarkerColor,
            MarkerCategory.Service => config.ServiceMarkerColor,
            MarkerCategory.Party => config.PartyMarkerColor,
            _ => config.PoiMarkerColor,
        };
        return config.IsGratis ? MapColour.MarkerColour(color, MapDrawContext.Current.ActiveColourPreset) : color;
    }

    internal static string Key(MarkerInfo marker) => $"{ImGui.GetID("flat-marker")}/{marker.IconId}/{marker.Position.X}/{marker.Position.Y}";

    private static float Size(MarkerInfo marker)
    {
        var context = MapDrawContext.Current;
        var size = context.Config.FlatMarkerSize * (context.Config.IconScale / .5f) * context.GetIconSetting(marker.IconId).Scale * context.GlobalScale;
        return size * MarkerVisual.Scale(marker);
    }

    internal static bool FitsFeather(MarkerInfo marker) => MapShape.ContainsSymbol(
        marker.Position + marker.Offset + ImGui.GetWindowPos(), new Vector2(Size(marker) * .6f + 3 * MapDrawContext.Current.GlobalScale));

    public static bool Draw(MarkerInfo marker)
    {
        var context = MapDrawContext.Current;
        var config = context.Config;
        var size = Size(marker);
        var center = marker.Position + marker.Offset + ImGui.GetWindowPos();
        var extent = new Vector2(size * .6f + 3 * context.GlobalScale);
        if (!MapShape.Contains(center - extent) || !MapShape.Contains(center + extent) ||
            !MapShape.Contains(center + new Vector2(extent.X, -extent.Y)) || !MapShape.Contains(center + new Vector2(-extent.X, extent.Y))) return false;
        var hitRadius = global::System.MathF.Max(size * .6f + 3 * context.GlobalScale, 8 * context.GlobalScale);
        ImGui.SetCursorScreenPos(center - new Vector2(hitRadius));
        ImGui.Dummy(new Vector2(hitRadius * 2));
        var hover = ImGui.IsItemHovered() ? HoverAnimation.Progress(Key(marker), true) : 0;
        var radius = size * .5f * (1 + .2f * hover);
        var list = ImGui.GetWindowDrawList();
        Shape(list, center, radius, Category(marker), Color(Category(marker)) * context.GetIconSetting(marker.IconId).Color);
        if (hover > 0) list.AddCircle(center, radius + 3 * context.GlobalScale,
            ImGui.GetColorU32(config.AccentColor with { W = hover }), 24, context.GlobalScale);
        return true;
    }

    public static void Shape(ImDrawListPtr list, Vector2 center, float radius, MarkerCategory category, Vector4 color, float alpha = 1)
    {
        var fill = ImGui.GetColorU32(color with { W = color.W * alpha });
        var light = ImGui.GetColorU32(new Vector4(1, 1, 1, .85f * alpha));
        var dark = ImGui.GetColorU32(GratisTheme.Outline with { W = .35f * alpha });
        var stroke = MapDrawContext.Current.GlobalScale;
        var x = new Vector2(radius, 0); var y = new Vector2(0, radius);
        void Contour(global::System.ReadOnlySpan<Vector2> points, uint ink, float width)
        {
            foreach (var point in points) list.PathLineTo(point);
            list.PathStroke(ink, ImDrawFlags.Closed, width);
        }
        switch (category) {
            case MarkerCategory.Transport:
                list.AddCircle(center, radius, light, 32, 5.5f * stroke);
                list.AddCircle(center, radius, dark, 32, 2.5f * stroke);
                list.AddCircle(center, radius, fill, 32, 1.5f * stroke);
                list.AddCircleFilled(center, radius * .32f, fill, 24);
                break;
            case MarkerCategory.Objective:
                global::System.ReadOnlySpan<Vector2> diamond = stackalloc Vector2[] { center - y, center + x, center + y, center - x };
                Contour(diamond, light, 4 * stroke);
                list.AddQuadFilled(center - y, center + x, center + y, center - x, fill);
                Contour(diamond, dark, stroke);
                break;
            case MarkerCategory.Link:
                global::System.ReadOnlySpan<Vector2> triangle = stackalloc Vector2[] { center - y, center + x + y, center - x + y };
                Contour(triangle, light, 4 * stroke);
                list.AddTriangleFilled(center - y, center + x + y, center - x + y, fill);
                Contour(triangle, dark, stroke);
                break;
            case MarkerCategory.Service:
                list.AddRect(center - new Vector2(radius), center + new Vector2(radius), light, 2 * stroke, ImDrawFlags.None, 4 * stroke);
                list.AddRectFilled(center - new Vector2(radius), center + new Vector2(radius), fill, 2 * stroke);
                list.AddRect(center - new Vector2(radius), center + new Vector2(radius), dark, 2 * stroke, ImDrawFlags.None, stroke);
                break;
            case MarkerCategory.Party:
                var arm = radius * .38f;
                Vector2 P(float px, float py) => center + new Vector2(px, py);
                global::System.ReadOnlySpan<Vector2> cross = stackalloc Vector2[] { P(-arm, -radius), P(arm, -radius), P(arm, -arm), P(radius, -arm),
                    P(radius, arm), P(arm, arm), P(arm, radius), P(-arm, radius),
                    P(-arm, arm), P(-radius, arm), P(-radius, -arm), P(-arm, -arm) };
                Contour(cross, light, 4 * stroke);
                list.AddRectFilled(center - new Vector2(radius, arm), center + new Vector2(radius, arm), fill);
                list.AddRectFilled(center - new Vector2(arm, radius), center + new Vector2(arm, radius), fill);
                Contour(cross, dark, stroke);
                break;
            default:
                list.AddCircle(center, radius, light, 32, 4 * stroke);
                list.AddCircleFilled(center, radius, fill, 32);
                list.AddCircle(center, radius, dark, 32, stroke);
                break;
        }
    }
}
