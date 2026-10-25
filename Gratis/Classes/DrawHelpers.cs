using System;
using System.Linq;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SeString = Dalamud.Game.Text.SeStringHandling.SeString;
#endif
using Gratis.Rendering;

namespace Gratis.Classes;

public class MarkerInfo
{
    public required Vector2 Position { get; set; }
    public required Vector2 Offset { get; set; }
    public required float Scale { get; set; }
    public uint? ObjectiveId { get; init; }
    public uint? DataId { get; set; }
    public bool? AetheryteAttuned { get; set; }
    public MarkerType MarkerType { get; set; }
    public MarkerCategory Category { get; set; }
    public MarkerIconFamily IconFamily { get; set; }
    public uint IconId { get; set; }
    public Func<string?>? PrimaryText { get; set; }
    public Func<string?>? SecondaryText { get; set; }
    public float? Radius { get; set; }
    public Vector4 RadiusColor { get; set; } = new Vector4(100 / 255.0f, 149 / 255.0f, 237 / 255.0f, 1.0f);
    public Vector4 RadiusOutlineColor { get; set; } = new Vector4(100 / 255.0f, 149 / 255.0f, 237 / 255.0f, 1.0f);
    public Action? OnRightClicked { get; set; }
    public Action? OnLeftClicked { get; set; }
    public bool IsDynamicMarker { get; init; }
    public bool IsRelevant { get; set; } = true;
    public bool IsTarget { get; init; }
    public bool IsFlag { get; init; }
    public bool IsTracked { get; init; }
    public bool IsObjective { get; set; }
    public bool IsHovered { get; internal set; }
    public bool IsSpotlighted { get; internal set; }
    public uint MotionRevision { get; set; }
}

public static class DrawHelpers
{
    private static bool DebugMode => MapDrawContext.Current.Config.DebugMode;

    public const uint QuestionMarkIcon = 60071;

#if !GRATIS_PREVIEW
    public static Vector2 GetMapOffsetVector() => GetRawMapOffsetVector() * GetMapScaleFactor();

    public static unsafe Vector2 GetRawMapOffsetVector() => new(AgentMap.Instance()->SelectedOffsetX, AgentMap.Instance()->SelectedOffsetY);

    public static unsafe float GetMapScaleFactor() => MapDrawContext.Current.MapSizeFactor;

    public static Vector2 GetMapCenterOffsetVector() => new(1024.0f, 1024.0f);

    public static Vector2 GetCombinedOffsetVector() => -GetMapOffsetVector() + GetMapCenterOffsetVector();

#endif

    public static void DrawMapMarker(MarkerInfo markerInfo)
    {
        if (markerInfo.IconId is 0) return;

        var context = MapDrawContext.Current;
        if (context.Config.IsGratis) {
            markerInfo.IconId = markerInfo.IconId switch {
                >= 60483 and <= 60494 when markerInfo.IsObjective || markerInfo.MarkerType is MarkerType.Fate or MarkerType.Stellar => QuestionMarkIcon,
                60091 => 61731,
                _ => markerInfo.IconId,
            };
            markerInfo.IsSpotlighted = context.Spotlight?.Matches(markerInfo) == true;
            context.Visibility.BeginFrame(context);
            if (!context.PrepareMarker(markerInfo) && context.Spotlight?.Matches(markerInfo) != true) return;
            context.Visibility.Count(markerInfo);
            context.EdgePointers.Observe(markerInfo);
            context.MarkerTargets.Observe(markerInfo);
        }

        if (!MapShape.Contains(markerInfo.Position + markerInfo.Offset + ImGui.GetWindowPos())) return;

        if (markerInfo.Position.X < 0.0f || markerInfo.Position.X > 2048.0f * markerInfo.Scale || markerInfo.Position.Y < 0.0f ||
            markerInfo.Position.Y > 2048.0f * markerInfo.Scale)
            return;

        markerInfo.IconId = markerInfo.IconId switch
        {
            >= 60483 and <= 60494 when !MapDrawContext.Current.Config.IsGratis || markerInfo.IsObjective ||
                markerInfo.MarkerType is MarkerType.Fate or MarkerType.Stellar => QuestionMarkIcon,

            60091 => 61731,

            _ => markerInfo.IconId,
        };

        if (DebugMode) {
            markerInfo.SecondaryText = markerInfo.PrimaryText;
            markerInfo.PrimaryText = () => $"[Debug] IconId: {markerInfo.IconId}";
        }

        if (!context.Config.IsGratis && !context.PrepareMarker(markerInfo)) return;
        if (context.Config.IsGratis) {
            var center = markerInfo.Position + markerInfo.Offset + ImGui.GetWindowPos();
            var half = MarkerVisual.HalfSize(markerInfo);
            markerInfo.IsHovered = ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(center - half, center + half, false);
        }
        context.Motion.Observe(markerInfo);

        if (context.Config.IsGratis && context.Visibility.Visual(markerInfo).X <= .001f) return;

        if (context.Markers.Enqueue(markerInfo)) return;
        DrawPreparedMarker(markerInfo);
    }

    internal static void DrawPreparedMarker(MarkerInfo markerInfo)
    {
        var alpha = MarkerVisual.Alpha(markerInfo);
        if (markerInfo.IsFlag) alpha *= MapDrawContext.Current.FlagArrival.FlagAlpha;
        alpha *= MapDrawContext.Current.Spotlight?.MarkerAlpha(markerInfo) ?? 1;
        if (alpha <= .001f) return;
        SpotlightDrawing.DrawMarkerGlow(markerInfo);
        if (alpha < 1) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * alpha);
        try { DrawPreparedContents(markerInfo); }
        finally { if (alpha < 1) ImGui.PopStyleVar(); }
    }

    private static void DrawPreparedContents(MarkerInfo markerInfo)
    {
        var context = MapDrawContext.Current;

        var attunementAlpha = AetheryteAttunementDrawing.Opacity(markerInfo);
        using (var attunement = FrameScopes.PushStyle(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * attunementAlpha, attunementAlpha < 1)) {
            if (MarkerVisual.UseFlatMarker(markerInfo)) {
                if (MapShape.FeatherWidth > 0 && !FlatMarkerDrawing.FitsFeather(markerInfo)) return;
                DrawRadiusUnderlay(markerInfo);
                if (!FlatMarkerDrawing.Draw(markerInfo)) return;
            }
            else if (!DrawIcon(markerInfo)) return;
        }
        AetheryteAttunementDrawing.Ring(markerInfo);
        context.Trace?.DrawnMarkers.Add(markerInfo);
        if (!context.WindowMoveInputBlocked) {
            ProcessInteractions(markerInfo);
            DrawTooltip(markerInfo);
        }
    }

    private static unsafe void DrawRadiusUnderlay(MarkerInfo markerInfo)
    {
        if (markerInfo is not { Radius: { } markerRadius and > 1.0f }) return;

        var center = markerInfo.Position + markerInfo.Offset + ImGui.GetWindowPos();
        var radius = markerRadius * markerInfo.Scale * MapDrawContext.Current.MapSizeFactor;
        if (!MapShape.ContainsSymbol(center, new Vector2(radius + MapDrawContext.Current.GlobalScale))) return;
        var fillColor = ImGui.GetColorU32(markerInfo.RadiusColor with { W = MapDrawContext.Current.Config.AreaColor.W });
        var radiusColor = ImGui.GetColorU32(markerInfo.RadiusOutlineColor with { W = MapDrawContext.Current.Config.AreaOutlineColor.W });

        if (MapDrawContext.Current.Config.IsGratis) {
            var color = FlatMarkerDrawing.Color(FlatMarkerDrawing.Category(markerInfo));
            fillColor = ImGui.GetColorU32(color with { W = MathF.Min(MapDrawContext.Current.Config.AreaColor.W, .10f) });
            radiusColor = ImGui.GetColorU32(color with { W = MathF.Min(MapDrawContext.Current.Config.AreaOutlineColor.W, .28f) });
            ImGui.GetWindowDrawList().AddCircleFilled(center, radius, fillColor);
            ImGui.GetWindowDrawList().AddCircle(center, radius, radiusColor, 0, MapDrawContext.Current.GlobalScale);
            return;
        }

        ImGui.GetWindowDrawList().AddCircleFilled(center, radius, fillColor);
        ImGui.GetWindowDrawList().AddCircle(center, radius, radiusColor, 0, 3.0f);
    }

    private static bool DrawIcon(MarkerInfo markerInfo)
    {
        var texture = MapDrawContext.Current.GetIcon(markerInfo.IconId);
        var scale = MarkerVisual.Scale(markerInfo);

        var iconScale = MapDrawContext.Current.Config.IconScale;

        if (markerInfo.IconId is 60401 or 60402) {
            scale *= 2.0f;
        }

        if (IsRegionIcon(markerInfo.IconId)) {
            if (!MapDrawContext.Current.Config.IsGratis || !MapDrawContext.Current.Config.Markers.ScaleWithZoom) scale = markerInfo.Scale;
            iconScale = 0.42f;
        }

        ImGui.SetCursorPos(markerInfo.Position + markerInfo.Offset - texture.Size * iconScale / 2.0f * scale * MapDrawContext.Current.GetIconSetting(markerInfo.IconId).Scale);
        var cursorScreenPos = ImGui.GetCursorScreenPos();
        var iconSize = texture.Size * scale * iconScale * MapDrawContext.Current.GetIconSetting(markerInfo.IconId).Scale;
        if (!MapShape.ContainsSymbol(cursorScreenPos + iconSize / 2, iconSize / 2)) return false;

        DrawRadiusUnderlay(markerInfo);
        if (MapDrawContext.Current.Config.IsGratis) {
            var list = ImGui.GetWindowDrawList();
            var step = MapDrawContext.Current.GlobalScale;
            var ink = ImGui.GetColorU32(new Vector4(0, 0, 0, .8f * MapDrawContext.Current.GetIconSetting(markerInfo.IconId).Color.W));
            foreach (var direction in IconContourDirections) {
                var offset = direction * step;
                list.AddImage(texture.Handle, cursorScreenPos + offset, cursorScreenPos + iconSize + offset, Vector2.Zero, Vector2.One, ink);
            }
        }
        ImGui.Image(texture.Handle, iconSize, Vector2.Zero, Vector2.One, MapDrawContext.Current.GetIconSetting(markerInfo.IconId).Color);
        MapDrawContext.Current.MarkerRendering.Record(MarkerDrawPath.GameIcon, markerInfo);

        if (DebugMode) {
            foreach (var x in Enumerable.Range(-1, 3)) {
                foreach (var y in Enumerable.Range(-1, 3)) {
                    ImGui.GetWindowDrawList().AddRect(cursorScreenPos + new Vector2(x, y), cursorScreenPos + iconSize, ImGui.GetColorU32(Vector4.One), 3.0f);
                }
            }

            ImGui.GetWindowDrawList().AddRect(cursorScreenPos, cursorScreenPos + iconSize, ImGui.GetColorU32(new Vector4(1, 0, 0, 1)), 3.0f);
        }
        return true;
    }

    private static readonly Vector2[] IconContourDirections = [new(-1, 0), new(1, 0), new(0, -1), new(0, 1)];

#if !GRATIS_PREVIEW
    public static void DrawText(MarkerInfo markerInfo, SeString text) => DrawText(markerInfo, text.ToString());
#endif

    public static void DrawText(MarkerInfo markerInfo, string text)
    {
        if (MapDrawContext.Current.Config.IsGratis) {
            DrawCrispText(markerInfo, text);
            return;
        }
        using var largeFont = MapDrawContext.Current.PushLargeFont();
        ImGui.SetWindowFontScale(markerInfo.Scale);

        var textSize = ImGui.CalcTextSize(text);
        var drawPosition = markerInfo.Position + markerInfo.Offset + ImGui.GetWindowPos() - textSize / 2.0f;

        drawPosition = new Vector2(MathF.Round(drawPosition.X), MathF.Round(drawPosition.Y));
        if (!MapShape.ContainsSymbol(drawPosition + textSize / 2, textSize / 2 + Vector2.One)) {
            ImGui.SetWindowFontScale(1.0f);
            return;
        }

        if (MapDrawContext.Current.Config.DebugMode) {
            ImGui.GetWindowDrawList().AddCircleFilled(markerInfo.Position + markerInfo.Offset + ImGui.GetWindowPos(), 5.0f, ImGui.GetColorU32(new Vector4(1, 0, 0, 1)));
            ImGui.GetWindowDrawList().AddRect(drawPosition, drawPosition + textSize, ImGui.GetColorU32(new Vector4(0, 128 / 255.0f, 0, 1)), 3.0f);
        }

        foreach (var x in Enumerable.Range(-1, 3)) {
            foreach (var y in Enumerable.Range(-1, 3)) {
                if (x is 0 && y is 0) continue;

                ImGui.SetCursorScreenPos(drawPosition + new Vector2(x, y));
                ImGui.TextColored(new Vector4(0, 0, 0, 1), text);
            }
        }

        ImGui.SetCursorScreenPos(drawPosition);
        ImGui.TextColored(Vector4.One, text);

        ImGui.SetWindowFontScale(1.0f);
    }

    private static void DrawCrispText(MarkerInfo marker, string text)
    {
        using var font = MapDrawContext.Current.GetChromeFont(96 * marker.Scale, ChromeFontRole.Body)?.Push();
        var size = TextDrawing.Measure(text);
        var position = TextDrawing.Snap(marker.Position + marker.Offset + ImGui.GetWindowPos() - size / 2);
        if (!MapShape.ContainsSymbol(position + size / 2, size / 2 + Vector2.One)) return;
        var list = ImGui.GetWindowDrawList();
        for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
                if (x != 0 || y != 0) TextDrawing.Draw(list, position + new Vector2(x, y), ImGui.GetColorU32(new Vector4(0, 0, 0, .9f)), text);
        TextDrawing.Draw(list, position, ImGui.GetColorU32(Vector4.One), text);
    }

    private static void ProcessInteractions(MarkerInfo markerInfo)
    {
        if (MapDrawContext.Current.Config is { IsGratis: true, GratisContextMenu: true }) {
            MapDrawContext.Current.ContextMenu.Observe(markerInfo);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) return;
        }
        if (MapDrawContext.Current.GetIconSetting(markerInfo.IconId) is not { AllowClick: true }) return;

        if (markerInfo is { OnRightClicked: { } rightClickAction } && ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
            rightClickAction.Invoke();
        }

        if (markerInfo is { OnLeftClicked: { } leftClickAction } && ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
            leftClickAction.Invoke();
        }
    }

    private static unsafe void DrawTooltip(MarkerInfo markerInfo)
    {
        if (MapDrawContext.Current.GetIconSetting(markerInfo.IconId) is { AllowTooltip: false } && !DebugMode) {
            return;
        }

        var isActivatedViaRadius = false;

        if (markerInfo is { Radius: { } sameRadius and > 1.0f }) {
            var center = markerInfo.Position + markerInfo.Offset + ImGui.GetWindowPos();
            var radius = sameRadius * markerInfo.Scale * MapDrawContext.Current.MapSizeFactor;

            if ((MapShape.FeatherWidth <= 0 || MapShape.ContainsSymbol(center, new Vector2(radius + MapDrawContext.Current.GlobalScale)) &&
                MapShape.Contains(ImGui.GetMousePos())) && MapDrawContext.Current.IsRadiusHovered(center, radius)) {
                isActivatedViaRadius = true;
            }
        }

        if (isActivatedViaRadius || ImGui.IsItemHovered()) {
            if (markerInfo.PrimaryText?.Invoke() is { Length: > 0 } primaryText) {
                if (MapDrawContext.Current.Config.IsGratis && MapDrawContext.Current.Config.HoverCardStyle == 0) {
                    var category = FlatMarkerDrawing.Category(markerInfo);
                    var label = FlatMarkerDrawing.Label(markerInfo);
                    var secondary = markerInfo.SecondaryText?.Invoke();
                    HoverCardDrawing.Draw(FlatMarkerDrawing.Key(markerInfo), primaryText,
                        PlayerDrawing.IsPlayerMarker(markerInfo) ? secondary :
                            string.IsNullOrWhiteSpace(secondary) ? label : $"{label}\n{secondary}",
                        category, markerInfo);
                    return;
                }
#if GRATIS_PREVIEW
                ImGui.BeginTooltip();
#else
                using var tooltip = ImRaii.Tooltip();
#endif

                ImGui.Image(MapDrawContext.Current.GetIcon(markerInfo.IconId).Handle, new Vector2(32.0f) * MapDrawContext.Current.GlobalScale);

                ImGui.SameLine();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 7.5f * MapDrawContext.Current.GlobalScale);
                var cursorPosition = ImGui.GetCursorPos();
                ImGui.Text(primaryText);

                if (markerInfo.SecondaryText?.Invoke() is { Length: > 0 } secondaryText) {
                    ImGui.SameLine();
                    ImGui.SetCursorPos(cursorPosition);
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(128 / 255.0f, 128 / 255.0f, 128 / 255.0f, 1));
                    ImGui.TextUnformatted($"\n{secondaryText}");
                    ImGui.PopStyleColor();
                }
#if GRATIS_PREVIEW
                ImGui.EndTooltip();
#endif
            }
        }
    }

    public static bool IsDisallowedIcon(uint iconId) =>
        iconId switch
        {
            60091 => true,
            _ when IsRegionIcon(iconId) => true,
            _ => false,
        };

    public static bool IsRegionIcon(uint iconId) =>
        iconId switch
        {
            >= 63200 and < 63900 => true,
            >= 62620 and < 62800 => true,
            _ => false,
        };
}
