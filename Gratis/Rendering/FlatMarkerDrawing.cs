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
        return MarkerIcon.Category(MarkerIcon.Family(marker));
    }

    public static string Label(MarkerInfo marker) => MarkerIcon.Family(marker) switch {
        MarkerIconFamily.Fate or MarkerIconFamily.FateBoss or MarkerIconFamily.FateCollect or MarkerIconFamily.FateDefend or
        MarkerIconFamily.FateEscort or MarkerIconFamily.FateSpecial => "FATE / event",
        MarkerIconFamily.Flag => "Flag",
        MarkerIconFamily.Treasure => "Treasure",
        MarkerIconFamily.Gathering or MarkerIconFamily.Botany or MarkerIconFamily.Fishing => "Gathering",
        _ => Label(Category(marker)),
    };

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

    internal static string Key(MarkerInfo marker) =>
        $"{ImGui.GetID("flat-marker")}/{marker.IconId}/{(int)marker.MarkerType}/{marker.ObjectiveId}/{marker.DataId}";

    private static float Size(MarkerInfo marker)
    {
        return MarkerVisual.FlatSize(marker);
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
        var hitRadius = MarkerVisual.HalfSize(marker).X;
        ImGui.SetCursorScreenPos(center - new Vector2(hitRadius));
        ImGui.Dummy(new Vector2(hitRadius * 2));
        var hover = ImGui.IsItemHovered() ? HoverAnimation.Progress(Key(marker), true) : 0;
        var radius = size * .5f * (1 + .2f * hover);
        var list = ImGui.GetWindowDrawList();
        if (context.Visibility.State(MarkerVisibility.Group(marker), MarkerVisibility.IsPriority(marker)) == MarkerVisibilityState.Loud)
            list.AddCircle(center, radius + 2 * context.GlobalScale, ImGui.GetColorU32(config.AccentColor with { W = .25f }), 24, context.GlobalScale);
        var path = Symbol(list, center, radius, marker);
        context.MarkerRendering.Record(path, marker, radius * 1.9f);
        if (hover > 0) list.AddCircle(center, radius + 3 * context.GlobalScale,
            ImGui.GetColorU32(config.AccentColor with { W = hover }), 24, context.GlobalScale);
        return true;
    }

    public static MarkerDrawPath Symbol(ImDrawListPtr list, Vector2 center, float radius, MarkerInfo marker, float alpha = 1)
    {
        if (MarkerIcon.Family(marker) is MarkerIconFamily.Neutral or MarkerIconFamily.Hunt) {
            var scale = MapDrawContext.Current.GlobalScale;
            var neutral = GratisTheme.Secondary * MapDrawContext.Current.GetIconSetting(marker.IconId).Color;
            radius *= .42f;
            list.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(1, 1, 1, .55f * alpha)), 24, 4 * scale);
            list.AddCircleFilled(center, radius, ImGui.GetColorU32(neutral with { W = neutral.W * alpha }), 24);
            list.AddCircle(center, radius, ImGui.GetColorU32(GratisTheme.Outline with { W = .35f * alpha }), 24, scale);
            return MarkerDrawPath.Dot;
        }
        if (PlayerDrawing.IsPlayerMarker(marker)) {
            PlayerDrawing.DrawDot(list, center, radius, MarkerIcon.Family(marker), MapDrawContext.Current.GetIconSetting(marker.IconId).Color, alpha);
            return MarkerDrawPath.Dot;
        }
        var color = Color(Category(marker)) * MapDrawContext.Current.GetIconSetting(marker.IconId).Color;
        if (MapDrawContext.Current.Config.Markers.IconStyle != MarkerIconStyle.Shapes)
            return ContextualMarkerDrawing.Draw(list, center, radius, marker, color, alpha);
        Shape(list, center, radius, Category(marker), color, alpha);
        return MarkerDrawPath.Shape;
    }

    public static void Shape(ImDrawListPtr list, Vector2 center, float radius, MarkerCategory category, Vector4 color, float alpha = 1)
    {
        var fill = ImGui.GetColorU32(color with { W = color.W * alpha });
        var light = ImGui.GetColorU32(new Vector4(1, 1, 1, .85f * alpha));
        var dark = ImGui.GetColorU32(new Vector4(.025f, .03f, .045f, .95f * alpha));
        var stroke = MapDrawContext.Current.GlobalScale;
        var x = new Vector2(radius, 0); var y = new Vector2(0, radius);
        void Contour(global::System.ReadOnlySpan<Vector2> points, uint ink, float width)
        {
            foreach (var point in points) list.PathLineTo(point);
            list.PathStroke(ink, ImDrawFlags.Closed, width);
        }
        switch (category) {
            case MarkerCategory.Transport:
                list.AddCircle(center, radius, dark, 32, 7 * stroke);
                list.AddCircle(center, radius, light, 32, 5.5f * stroke);
                list.AddCircle(center, radius, dark, 32, 2.5f * stroke);
                list.AddCircle(center, radius, fill, 32, 1.5f * stroke);
                list.AddCircleFilled(center, radius * .32f, fill, 24);
                break;
            case MarkerCategory.Objective:
                global::System.ReadOnlySpan<Vector2> diamond = stackalloc Vector2[] { center - y, center + x, center + y, center - x };
                Contour(diamond, dark, 6 * stroke);
                Contour(diamond, light, 4 * stroke);
                list.AddQuadFilled(center - y, center + x, center + y, center - x, fill);
                Contour(diamond, dark, stroke);
                break;
            case MarkerCategory.Link:
                global::System.ReadOnlySpan<Vector2> triangle = stackalloc Vector2[] { center - y, center + x + y, center - x + y };
                Contour(triangle, dark, 6 * stroke);
                Contour(triangle, light, 4 * stroke);
                list.AddTriangleFilled(center - y, center + x + y, center - x + y, fill);
                Contour(triangle, dark, stroke);
                break;
            case MarkerCategory.Service:
                list.AddRect(center - new Vector2(radius), center + new Vector2(radius), dark, 2 * stroke, ImDrawFlags.None, 6 * stroke);
                list.AddRect(center - new Vector2(radius), center + new Vector2(radius), light, 2 * stroke, ImDrawFlags.None, 4 * stroke);
                list.AddRectFilled(center - new Vector2(radius), center + new Vector2(radius), fill, 2 * stroke);
                list.AddRect(center - new Vector2(radius), center + new Vector2(radius), dark, 2 * stroke, ImDrawFlags.None, stroke);
                break;
            case MarkerCategory.Party:
                list.AddCircleFilled(center, radius * .5f, fill, 24);
                list.AddCircle(center, radius * .5f + .75f * stroke, light, 24, 1.5f * stroke);
                break;
            default:
                list.AddCircle(center, radius, dark, 32, 6 * stroke);
                list.AddCircle(center, radius, light, 32, 4 * stroke);
                list.AddCircleFilled(center, radius, fill, 32);
                list.AddCircle(center, radius, dark, 32, stroke);
                break;
        }
    }
}
