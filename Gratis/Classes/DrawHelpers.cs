using System;
using System.Linq;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif
using Gratis.Rendering;

namespace Gratis.Classes;

public static partial class DrawHelpers {
    private static bool DebugMode => MapDrawContext.Current.Config.DebugMode;

    public const uint QuestionMarkIcon = 60071;

    public static void DrawMapMarker(MarkerInfo marker) {
        if (marker.IconId is 0)
            return;

        var context = MapDrawContext.Current;
        context.SynchronizePvp();
        context.Pvp.BeginFrame(context.IsPvP, ImGui.GetFrameCount());
        if (!context.Pvp.Consider(marker))
            return;
        if (context.Config.IsGratis) {
            marker.IconId = RemapIcon(marker);
            marker.IsSpotlighted = context.Spotlight?.Matches(marker) == true;
            context.Visibility.BeginFrame(context);
            if (!context.PrepareMarker(marker) && context.Spotlight?.Matches(marker) != true)
                return;
            context.Visibility.Count(marker);
            if (!context.IsPvP || !PvpMapPolicy.IsPlayer(marker)) {
                context.EdgePointers.Observe(marker);
                context.MarkerTargets.Observe(marker);
            }
        }

        if (!MapShape.Contains(marker.Position + marker.Offset + ImGui.GetWindowPos()))
            return;

        if (marker.Position.X < 0.0f || marker.Position.X > 2048.0f * marker.Scale || marker.Position.Y < 0.0f
            || marker.Position.Y > 2048.0f * marker.Scale)
            return;

        marker.IconId = RemapIcon(marker, includeClassic: true);

        if (DebugMode)
            PrepareDebugMarker(marker);

        if (!context.Config.IsGratis && !context.PrepareMarker(marker))
            return;
        if (context.Config.IsGratis) {
            var center = marker.Position + marker.Offset + ImGui.GetWindowPos();
            var half = MarkerVisual.HalfSize(marker);
            marker.IsHovered = !context.QuestLog.InputActive && ImGui.IsWindowHovered()
                && ImGui.IsMouseHoveringRect(center - half, center + half, false);
        }
        context.Motion.Observe(marker);

        if (context.Config.IsGratis && context.Visibility.Visual(marker).X <= .001f)
            return;

        if (context.Markers.Enqueue(marker))
            return;
        DrawPreparedMarker(marker);
    }

    private static void PrepareDebugMarker(MarkerInfo marker) {
        marker.SecondaryText = marker.PrimaryText;
        marker.PrimaryText = () => $"[Debug] IconId: {marker.IconId}";
    }

    private static uint RemapIcon(MarkerInfo marker, bool includeClassic = false) => marker.IconId switch {
        >= 60483 and <= 60494 when includeClassic && !MapDrawContext.Current.Config.IsGratis || marker.IsObjective
            || marker.MarkerType is MarkerType.Fate or MarkerType.Stellar => QuestionMarkIcon,
        60091 => 61731,
        _ => marker.IconId,
    };

    internal static void DrawPreparedMarker(MarkerInfo marker) {
#if GRATIS_PREVIEW
        using var benchmark = BenchmarkProbe.Measure(BenchmarkSection.MarkerDraw);
#endif
        if (!PvpMapPolicy.Allows(MapDrawContext.Current.IsPvP, marker))
            return;
        var alpha = MarkerVisual.Alpha(marker);
        if (marker.IsFlag)
            alpha *= MapDrawContext.Current.FlagArrival.FlagAlpha;
        alpha *= MapDrawContext.Current.Spotlight?.MarkerAlpha(marker) ?? 1;
        if (MapDrawContext.Current.Config.IsGratis && !marker.IsHovered)
            alpha *= MarkerVisual.Opacity;
        if (alpha <= .001f)
            return;
        SpotlightDrawing.DrawMarkerGlow(marker);
        MapDrawContext.Current.QuestLog.DrawMarkerHover(marker);
        if (alpha < 1)
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * alpha);
        try {
            DrawPreparedContents(marker);
        }
        finally { if (alpha < 1) ImGui.PopStyleVar(); }
    }

    private static void DrawPreparedContents(MarkerInfo marker) {
        var context = MapDrawContext.Current;

        var attunementAlpha = AetheryteAttunementDrawing.Opacity(marker);
        using (var attunement = FrameScopes.PushStyle(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * attunementAlpha,
            attunementAlpha < 1)) {
            if (MarkerVisual.UseFlatMarker(marker)) {
                if (MapShape.FeatherWidth > 0 && !FlatMarkerDrawing.FitsFeather(marker))
                    return;
                DrawRadiusUnderlay(marker);
                if (!FlatMarkerDrawing.Draw(marker))
                    return;
            }
            else if (!DrawIcon(marker))
                return;
        }
        AetheryteAttunementDrawing.Ring(marker);
        context.Pvp.RecordDrawn(marker);
        context.Trace?.DrawnMarkers.Add(marker);
        if (!context.WindowMoveInputBlocked) {
            ProcessInteractions(marker);
            DrawTooltip(marker);
        }
    }

    private static unsafe void DrawRadiusUnderlay(MarkerInfo marker) {
        if (marker is not { Radius: { } markerRadius and > 1.0f })
            return;

        var center = marker.Position + marker.Offset + ImGui.GetWindowPos();
        var radius = markerRadius * marker.Scale * MapDrawContext.Current.MapSizeFactor;
        if (!MapShape.ContainsSymbol(center, new Vector2(radius + MapDrawContext.Current.GlobalScale)))
            return;
        var fillColor = ImGui.GetColorU32(marker.RadiusColor with { W = MapDrawContext.Current.Config.AreaColor.W });
        var radiusColor = ImGui.GetColorU32(
            marker.RadiusOutlineColor with { W = MapDrawContext.Current.Config.AreaOutlineColor.W });

        if (MapDrawContext.Current.Config.IsGratis) {
            var color = FlatMarkerDrawing.Color(FlatMarkerDrawing.Category(marker));
            fillColor = ImGui.GetColorU32(color with {
                W = MathF.Min(MapDrawContext.Current.Config.AreaColor.W,
                .10f)
            });
            radiusColor = ImGui.GetColorU32(color with {
                W = MathF.Min(MapDrawContext.Current.Config.AreaOutlineColor.W,
                .28f)
            });
            ImGui.GetWindowDrawList().AddCircleFilled(center, radius, fillColor);
            ImGui.GetWindowDrawList().AddCircle(center, radius, radiusColor, 0, MapDrawContext.Current.GlobalScale);
            return;
        }

        ImGui.GetWindowDrawList().AddCircleFilled(center, radius, fillColor);
        ImGui.GetWindowDrawList().AddCircle(center, radius, radiusColor, 0, 3.0f);
    }

    private static bool DrawIcon(MarkerInfo marker) {
        var texture = MapDrawContext.Current.GetIcon(marker.IconId);
        var scale = MarkerVisual.Scale(marker);

        var iconScale = MapDrawContext.Current.Config.IconScale;

        if (marker.IconId is 60401 or 60402) {
            scale *= 2.0f;
        }

        if (IsRegionIcon(marker.IconId)) {
            if (!MapDrawContext.Current.Config.IsGratis)
                scale = marker.Scale;
            else
                scale = ViewportMath.SymbolScale(scale);
            iconScale = 0.42f;
        }

        ImGui.SetCursorPos(marker.Position + marker.Offset
            - texture.Size * iconScale / 2.0f * scale);
        var cursorScreenPos = ImGui.GetCursorScreenPos();
        var iconSize = texture.Size * scale * iconScale;
        if (!MapShape.ContainsSymbol(cursorScreenPos + iconSize / 2, iconSize / 2))
            return false;

        DrawRadiusUnderlay(marker);
        if (MapDrawContext.Current.Config.IsGratis) {
            var list = ImGui.GetWindowDrawList();
            var step = MapDrawContext.Current.GlobalScale;
            var ink = ImGui.GetColorU32(new Vector4(0, 0, 0, .8f));
            foreach (var direction in IconContourDirections) {
                var offset = direction * step;
                list.AddImage(texture.Handle, cursorScreenPos + offset, cursorScreenPos + iconSize + offset,
                    Vector2.Zero, Vector2.One, ink);
            }
        }
        ImGui.Image(texture.Handle, iconSize, Vector2.Zero, Vector2.One, Vector4.One);
        MapDrawContext.Current.MarkerRendering.Record(MarkerDrawPath.GameIcon, marker);

        if (DebugMode) {
            foreach (var x in Enumerable.Range(-1, 3)) {
                foreach (var y in Enumerable.Range(-1, 3)) {
                    ImGui.GetWindowDrawList().AddRect(cursorScreenPos + new Vector2(x, y), cursorScreenPos + iconSize,
                        ImGui.GetColorU32(Vector4.One), 3.0f);
                }
            }

            ImGui.GetWindowDrawList().AddRect(cursorScreenPos, cursorScreenPos + iconSize,
                ImGui.GetColorU32(new Vector4(1, 0, 0, 1)), 3.0f);
        }
        return true;
    }

    private static readonly Vector2[] IconContourDirections = [new(-1, 0), new(1, 0), new(0, -1), new(0, 1)];

    public static bool IsDisallowedIcon(uint iconId) => iconId switch {
        60091 => true,
        _ when IsRegionIcon(iconId) => true,
        _ => false,
    };

    public static bool IsRegionIcon(uint iconId) => iconId switch {
        >= 63200 and < 63900 => true,
        >= 62620 and < 62800 => true,
        _ => false,
    };
}
