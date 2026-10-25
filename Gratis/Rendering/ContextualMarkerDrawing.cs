using System;
using System.Numerics;
using Gratis.Classes;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class ContextualMarkerDrawing
{
    private static readonly Vector4 Gold = new(.91f, .72f, .32f, 1);
    private static readonly Vector4 Muted = new(.79f, .72f, .49f, 1);
    private static readonly Vector4 Contour = new(.025f, .03f, .045f, .95f);
    private static readonly Vector4 Purple = new(.65f, .48f, .91f, 1);
    private static readonly Vector4 Blue = new(.31f, .64f, .93f, 1);

    public static bool Draw(ImDrawListPtr list, Vector2 center, float radius, MarkerInfo marker, Vector4 color, float alpha)
    {
        var family = MarkerIcon.Family(marker);
        var glyph = MarkerIcon.Glyph(family);
        if (!GratisIcons.Available(glyph, radius * 1.9f)) return false;
        var tint = MapDrawContext.Current.GetIconSetting(marker.IconId).Color;
        if (family is MarkerIconFamily.Aetheryte or MarkerIconFamily.Aethernet) {
            Crystal(list, center, radius, family == MarkerIconFamily.Aethernet, color, alpha);
            return true;
        }
        if (family == MarkerIconFamily.Quest) {
            MarkerIcon.TryQuest(marker.IconId, out var quest);
            if (marker.IconId is 60071 or 60094 or 60095 or 60096 or 60098 or 60099)
                quest = quest with { State = QuestMarkerState.InProgress };
            Quest(list, center, radius, quest, tint, alpha);
            return true;
        }
        var fate = family is MarkerIconFamily.Fate or MarkerIconFamily.FateBoss or MarkerIconFamily.FateCollect or
            MarkerIconFamily.FateDefend or MarkerIconFamily.FateEscort or MarkerIconFamily.FateSpecial;
        if (fate) color = (marker.IconId is 60507 or 60728 ? Muted : Purple) * tint;
        var stroke = MapDrawContext.Current.GlobalScale;
        var light = Ink(new Vector4(1, 1, 1, .85f), alpha);
        var dark = Ink(Contour, alpha);
        var fill = Ink(Vector4.Lerp(GratisTheme.Outline with { W = 1 }, color, .42f) with { W = color.W }, alpha);
        if (fate) {
            list.AddCircle(center, radius, dark, 24, 6 * stroke);
            list.AddCircle(center, radius, light, 24, 4 * stroke);
            list.AddCircleFilled(center, radius, fill, 24);
            list.AddCircle(center, radius, dark, 24, stroke);
        }
        else {
            var half = new Vector2(radius);
            list.AddRect(center - half, center + half, dark, 2.5f * stroke, ImDrawFlags.None, 6 * stroke);
            list.AddRect(center - half, center + half, light, 2.5f * stroke, ImDrawFlags.None, 4 * stroke);
            list.AddRectFilled(center - half, center + half, fill, 2.5f * stroke);
            list.AddRect(center - half, center + half, dark, 2.5f * stroke, ImDrawFlags.None, stroke);
        }
        GratisIcons.Draw(list, center, glyph, new Vector4(1, 1, 1, color.W * alpha), radius * 1.9f);
        return true;
    }

    private static uint Ink(Vector4 color, float alpha) => ImGui.GetColorU32(color with { W = color.W * alpha });

    private static void Polygon(ImDrawListPtr list, ReadOnlySpan<Vector2> points, Vector4 color, float alpha)
    {
        var scale = MapDrawContext.Current.GlobalScale;
        foreach (var point in points) list.PathLineTo(point);
        list.PathStroke(Ink(Contour, alpha), ImDrawFlags.Closed, 6 * scale);
        foreach (var point in points) list.PathLineTo(point);
        list.PathStroke(Ink(new Vector4(1, 1, 1, .85f), alpha), ImDrawFlags.Closed, 4 * scale);
        foreach (var point in points) list.PathLineTo(point);
        list.PathFillConvex(Ink(color, alpha));
        foreach (var point in points) list.PathLineTo(point);
        list.PathStroke(Ink(Contour, alpha), ImDrawFlags.Closed, 1.25f * scale);
    }

    private static void Crystal(ImDrawListPtr list, Vector2 center, float r, bool shard, Vector4 color, float alpha)
    {
        Vector2 P(float x, float y) => center + new Vector2(x, y) * r;
        ReadOnlySpan<Vector2> points = stackalloc Vector2[] { P(0, -1), P(.62f, -.12f), P(.42f, .54f), P(0, .75f), P(-.42f, .54f), P(-.62f, -.12f) };
        Polygon(list, points, color, alpha);
        list.AddTriangleFilled(P(0, -1), P(0, .72f), P(-.6f, -.12f), Ink(new Vector4(1, 1, 1, .24f), alpha));
        list.AddTriangleFilled(P(0, .72f), P(0, -.18f), P(.6f, -.12f), Ink(new Vector4(0, 0, 0, .16f), alpha));
        list.AddLine(P(-.56f, -.12f), P(0, .05f), Ink(new Vector4(1, 1, 1, .5f), alpha), .7f * MapDrawContext.Current.GlobalScale);
        if (!shard) {
            list.AddLine(P(-.6f, .72f), P(.6f, .72f), Ink(Gold, alpha), 2 * MapDrawContext.Current.GlobalScale);
            list.AddLine(P(-.35f, 1), P(.35f, 1), Ink(Gold, alpha), 1.5f * MapDrawContext.Current.GlobalScale);
        }
    }

    private static void Quest(ImDrawListPtr list, Vector2 center, float r, QuestMarker quest, Vector4 tint, float alpha)
    {
        var scale = MapDrawContext.Current.GlobalScale;
        var color = (quest.State == QuestMarkerState.Locked ? new Vector4(.68f, .31f, .29f, 1) :
            quest.State == QuestMarkerState.InProgress ? Muted : quest.Feature ? Blue : Gold) * tint;
        Span<Vector2> pin = stackalloc Vector2[15];
        for (var i = 0; i < 14; i++) {
            var angle = MathF.PI * (.75f - 1.5f * i / 13);
            pin[i] = center + new Vector2(MathF.Sin(angle) * .82f, -MathF.Cos(angle) * .82f - .12f) * r;
        }
        pin[14] = center + new Vector2(0, 1.05f * r);
        Polygon(list, pin, color, alpha);
        list.AddLine(center + new Vector2(-.38f, -.5f) * r, center + new Vector2(-.55f, .1f) * r,
            Ink(new Vector4(1, 1, 1, .25f), alpha), scale);
        var glyphCenter = center - new Vector2(0, .13f * r);
        var ink = quest.MainScenario ? new Vector4(.27f, .13f, .4f, 1) : new Vector4(.16f, .13f, .22f, 1);
        if (quest.State == QuestMarkerState.Complete) {
            GratisIcons.Draw(list, glyphCenter, GratisIcons.Check, new Vector4(.13f, .35f, .17f, tint.W * alpha), r * 1.8f);
        }
        else if (quest.Repeatable || quest.State == QuestMarkerState.Locked) {
            GratisIcons.Draw(list, glyphCenter, quest.State == QuestMarkerState.Locked ? GratisIcons.Lock : GratisIcons.Repeat,
                ink with { W = tint.W * alpha }, r * 1.9f);
        }
        else {
            using var font = MapDrawContext.Current.GetChromeFont(r * 2, ChromeFontRole.Title)?.Push();
            var text = quest.State == QuestMarkerState.Available ? "!" : "?";
            TextDrawing.Draw(list, glyphCenter - TextDrawing.Measure(text) / 2,
                Ink(ink with { W = tint.W }, alpha), text);
        }
        if (quest.MainScenario) {
            for (var i = -1; i <= 1; i++) {
                var start = center + new Vector2(i * .5f, -.95f) * r;
                list.AddLine(start, start + new Vector2(i * .16f, -.25f) * r, Ink(Gold * tint, alpha), scale);
            }
        }
        if (quest.Feature) {
            var badge = center + new Vector2(-.65f, .6f) * r;
            list.AddLine(badge - new Vector2(.29f * r, 0), badge + new Vector2(.29f * r, 0), Ink(Gold * tint, alpha), 2 * scale);
            list.AddLine(badge - new Vector2(0, .29f * r), badge + new Vector2(0, .29f * r), Ink(Gold * tint, alpha), 2 * scale);
        }
    }
}
