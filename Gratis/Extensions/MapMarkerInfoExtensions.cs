using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Gratis.Classes;
using Map = Lumina.Excel.Sheets.Map;
using MarkerInfo = Gratis.Classes.MarkerInfo;

namespace Gratis.Extensions;

public static class MapMarkerInfoExtensions {
    public static void Draw(this MapMarkerInfo marker, Vector2 offset, float scale) {
        var tooltipText = marker.MapMarker.Subtext.AsDalamudSeString();

        var markerInfo = new MarkerInfo {
            // Native marker coordinates use 1/16-pixel units around the map centre.
            // Add offset for drawing relative to map when it's moved around
            Position = new Vector2(marker.MapMarker.X, marker.MapMarker.Y) / 16.0f * scale
                + DrawHelpers.GetMapCenterOffsetVector() * scale,
            Offset = offset,
            Source = Rendering.MapMarkerSource.NativeStatic,
            Scale = scale,
            Radius = marker.MapMarker.Scale,
            RadiusColor = KnownColor.MediumPurple.Vector(),
            IconId = marker.MapMarker.IconId,
            DataId = marker.DataKey,
            IconFamily = marker.DataType switch {
                1 => Rendering.MarkerIconFamily.Exit,
                2 => Rendering.MarkerIconFamily.Duty,
                3 => Rendering.MarkerIconFamily.Aetheryte,
                4 => Rendering.MarkerIconFamily.Aethernet,
                _ => Rendering.MarkerIconFamily.Auto,
            },
            Category = marker.DataType switch {
                1 or 2 => Rendering.MarkerCategory.Link,
                3 or 4 => Rendering.MarkerCategory.Transport,
                _ => Rendering.MarkerCategory.Auto,
            },
            PrimaryText = GetMarkerPrimaryTooltip(marker, tooltipText),
            OnLeftClicked = Rendering.MapDrawContext.Current.Config.IsGratis && marker.DataType is 3 or 4 ? null
                : () => OnMarkerClicked(ref marker),
            SecondaryText = () => GetTooltip(ref marker),
        };

        if (marker.MapMarker.IconId is 0 && marker.MapMarker.Index is not 0) {
            TryDrawText(marker, markerInfo, tooltipText);
        }
        else {
            DrawHelpers.DrawMapMarker(markerInfo);
        }
    }

    private static void TryDrawText(MapMarkerInfo marker, MarkerInfo markerInfo, SeString tooltipText) {
        if (!System.SystemConfig.ShowTextLabels)
            return;

        var textTypeScalar = marker.MapMarker.SubtextStyle switch {
            1 => System.SystemConfig.LargeAreaTextScale,
            _ => System.SystemConfig.SmallAreaTextScale,
        };

        if (System.SystemConfig.ScaleTextWithZoom) {
            markerInfo.Scale *= textTypeScalar * 0.33f;
        }
        else {
            markerInfo.Scale = textTypeScalar * 0.33f;
        }

        var opacity = marker.MapMarker.SubtextStyle is 1 ? System.SystemConfig.LargeAreaTextOpacity
            : System.SystemConfig.SmallAreaTextOpacity;
        var fade = Rendering.MapDrawContext.Current.Config.IsGratis && float.IsFinite(opacity);
        if (fade && opacity <= .01f)
            return;
        if (fade)
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * Math.Clamp(opacity, 0, 1));
        DrawHelpers.DrawText(markerInfo, tooltipText);
        if (fade)
            ImGui.PopStyleVar();
    }

    private static void OnMarkerClicked(ref MapMarkerInfo marker) {
        if (Service.ClientState.IsPvP)
            return;
        switch (marker.DataType) {
            case 1:
                OnMapLinkMarkerClicked(ref marker);
                break;

            case 3:
                OnAetheryteClicked(ref marker);
                break;

            case 4:
                OnAethernetClicked(ref marker);
                break;
        }
    }

    private static void OnMapLinkMarkerClicked(ref MapMarkerInfo marker) {
        if (marker.DataKey is 0)
            return;
        if (DrawHelpers.IsDisallowedIcon(marker.MapMarker.IconId))
            return;

        System.IntegrationsController.OpenMap(marker.DataKey);
    }

    private static void OnAetheryteClicked(ref MapMarkerInfo marker) {
        if (marker.DataKey is 0)
            return;

        System.Teleporter.Teleport(marker.DataKey);
    }

    private static void OnAethernetClicked(ref MapMarkerInfo marker) {
        var aetheryte = GetAetheryteForAethernet(marker.DataKey);
        if (aetheryte is null)
            return;
        if (aetheryte.Value.RowId is 0)
            return;

        System.Teleporter.Teleport(aetheryte.Value.RowId);
    }

    private static string GetTooltip(ref MapMarkerInfo marker) {
        switch (marker.DataType) {
            case 1:
                return GetMapLinkTooltip(ref marker);

            case 2:
                return GetInstanceLinkTooltip(ref marker);

            case 3:
                return GetAetheryteTooltip(ref marker);

            case 4:
                return GetAethernetTooltip(ref marker);
        }

        return string.Empty;
    }

    private static string GetMapLinkTooltip(ref MapMarkerInfo marker) {
        if (marker.DataKey is 0)
            return string.Empty;
        if (DrawHelpers.IsDisallowedIcon(marker.MapMarker.IconId))
            return string.Empty;

        var map = Service.DataManager.GetExcelSheet<Map>().GetRow(marker.DataKey);
        var mapPlaceName = map.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;

        return $"Open Map {mapPlaceName}";
    }

    private static string GetInstanceLinkTooltip(ref MapMarkerInfo marker) {
        return $"Instance Link {marker.DataKey}";
    }

    private static string GetAetheryteTooltip(ref MapMarkerInfo marker) {
        if (marker.DataKey is 0)
            return string.Empty;

        var aetheryteTeleportCost = GetAetheryteTeleportGilCost(marker.DataKey);
        if (aetheryteTeleportCost is null)
            return "Not attuned to aetheryte";

        var aetheryte = Service.DataManager.GetExcelSheet<Aetheryte>().GetRow(marker.DataKey);
        var aetherytePlaceName = aetheryte.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
        var aetheryteCost = GetAetheryteTeleportCost(marker.DataKey);

        return $"Teleport to {aetherytePlaceName} {aetheryteCost}";
    }

    private static string GetAethernetTooltip(ref MapMarkerInfo marker) {
        if (marker.DataKey is 0)
            return string.Empty;

        var aetheryte = GetAetheryteForAethernet(marker.DataKey);
        if (aetheryte is null)
            return string.Empty;
        if (aetheryte.Value.RowId is 0)
            return string.Empty;

        var aetherytePlaceName = aetheryte.Value.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;

        return $"Teleport to {aetherytePlaceName} {GetAetheryteTeleportCost(aetheryte.Value.RowId)}";
    }

    private static Aetheryte
        ? GetAetheryteForAethernet(uint aethernetKey) => System.AetheryteAethernetCache.GetValue(aethernetKey);

    private static uint? GetAetheryteTeleportGilCost(uint aethernetKey) =>
        Service.AetheryteList.FirstOrDefault(entry => entry.AetheryteId == aethernetKey)?.GilCost;

    private static string GetAetheryteTeleportCost(uint targetDataKey) =>
        $"({GetAetheryteTeleportGilCost(targetDataKey) ?? 0:n0} {SeIconChar.Gil.ToIconChar()})";

    private static Func<string> GetMarkerPrimaryTooltip(MapMarkerInfo marker, SeString tooltipText) {
        if (DrawHelpers.IsDisallowedIcon(marker.MapMarker.IconId))
            return () => string.Empty;
        if (!System.SystemConfig.ShowMiscTooltips)
            return () => string.Empty;
        if (!tooltipText.TextValue.IsNullOrEmpty())
            return tooltipText.ToString;

        return marker.DataType switch {
            4 => () => Service.DataManager.GetExcelSheet<PlaceName>().GetRow(marker.DataKey).Name.ExtractText(),
            _ => () => System.TooltipCache.GetValue(marker.MapMarker.IconId) ?? string.Empty,
        };
    }
}