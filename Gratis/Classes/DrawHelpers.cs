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
    public MarkerType MarkerType { get; set; }
    public MarkerCategory Category { get; set; }
    public uint IconId { get; set; }
    public Func<string?>? PrimaryText { get; set; }
    public Func<string?>? SecondaryText { get; set; }
    public float? Radius { get; set; }
    public Vector4 RadiusColor { get; set; } = new Vector4(100 / 255.0f, 149 / 255.0f, 237 / 255.0f, 1.0f);
    public Vector4 RadiusOutlineColor { get; set; } = new Vector4(100 / 255.0f, 149 / 255.0f, 237 / 255.0f, 1.0f);
    public Action? OnRightClicked { get; set; }
    public Action? OnLeftClicked { get; set; }
    public bool IsDynamicMarker { get; init; }
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

        if (!MapShape.Contains(markerInfo.Position + markerInfo.Offset + ImGui.GetWindowPos())) return;

        if (markerInfo.Position.X < 0.0f || markerInfo.Position.X > 2048.0f * markerInfo.Scale || markerInfo.Position.Y < 0.0f ||
            markerInfo.Position.Y > 2048.0f * markerInfo.Scale)
            return;

        markerInfo.IconId = markerInfo.IconId switch
        {
            >= 60483 and <= 60494 => QuestionMarkIcon,

            60091 => 61731,

            _ => markerInfo.IconId,
        };

        if (DebugMode) {
            markerInfo.SecondaryText = markerInfo.PrimaryText;
            markerInfo.PrimaryText = () => $"[Debug] IconId: {markerInfo.IconId}";
        }

        var context = MapDrawContext.Current;
        if (!context.PrepareMarker(markerInfo)) return;

        if (context.Config.IsGratis && context.Config.MarkerStyle == 1) {
            if (MapShape.FeatherWidth > 0 && !FlatMarkerDrawing.FitsFeather(markerInfo)) return;
            DrawRadiusUnderlay(markerInfo);
            if (!FlatMarkerDrawing.Draw(markerInfo)) return;
        }
        else if (!DrawIcon(markerInfo)) return;
        ProcessInteractions(markerInfo);
        DrawTooltip(markerInfo);
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
        var scale = MapDrawContext.Current.Config.ScaleWithZoom ? markerInfo.Scale : 1.0f;

        var iconScale = MapDrawContext.Current.Config.IconScale;

        if (markerInfo.IconId is 60401 or 60402) {
            scale *= 2.0f;
        }

        if (IsRegionIcon(markerInfo.IconId)) {
            scale = markerInfo.Scale;
            iconScale = 0.42f;
        }

        ImGui.SetCursorPos(markerInfo.Position + markerInfo.Offset - texture.Size * iconScale / 2.0f * scale * MapDrawContext.Current.GetIconSetting(markerInfo.IconId).Scale);
        var cursorScreenPos = ImGui.GetCursorScreenPos();
        var iconSize = texture.Size * scale * iconScale * MapDrawContext.Current.GetIconSetting(markerInfo.IconId).Scale;
        if (!MapShape.ContainsSymbol(cursorScreenPos + iconSize / 2, iconSize / 2)) return false;

        DrawRadiusUnderlay(markerInfo);
        ImGui.Image(texture.Handle, iconSize, Vector2.Zero, Vector2.One, MapDrawContext.Current.GetIconSetting(markerInfo.IconId).Color);

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

#if !GRATIS_PREVIEW
    public static void DrawText(MarkerInfo markerInfo, SeString text) => DrawText(markerInfo, text.ToString());
#endif

    public static void DrawText(MarkerInfo markerInfo, string text)
    {
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

    private static void ProcessInteractions(MarkerInfo markerInfo)
    {
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
                    var secondary = markerInfo.SecondaryText?.Invoke();
                    HoverCardDrawing.Draw(FlatMarkerDrawing.Key(markerInfo), primaryText,
                        string.IsNullOrWhiteSpace(secondary) ? FlatMarkerDrawing.Label(category) : $"{FlatMarkerDrawing.Label(category)}\n{secondary}",
                        category);
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
