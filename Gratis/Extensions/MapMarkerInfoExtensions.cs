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
    // The native subtext was parsed into a SeString for every marker on every frame, which allocated heavily. The
    // parse is keyed by the text's own bytes, so a marker whose text changes simply parses again.
    private static readonly global::System.Collections.Generic.Dictionary<(int Hash, int Length), CachedText> Subtexts = [];

    // Everything derived from one subtext: the strings the old code rebuilt every frame (TextValue, ToString) and the
    // delegate that returns the tooltip, all computed once per distinct text instead of once per marker per frame.
    private sealed class CachedText {
        public readonly string Text;
        public readonly bool IsEmpty;
        public readonly Func<string?> Tooltip;

        public CachedText(SeString text) {
            Text = text.ToString();
            IsEmpty = text.TextValue.IsNullOrEmpty();
            Tooltip = () => Text;
        }
    }

    // The click, hover and fallback-tooltip delegates only read the marker's type, key and icon, so one set per distinct
    // (type, key, icon) is shared instead of closing over a fresh copy of the marker for every marker every frame.
    private sealed class NativeCallbacks {
        public readonly global::System.Action OnClicked;
        public readonly Func<string?> Secondary;
        public readonly Func<string?> FallbackPrimary;

        public NativeCallbacks(ushort dataType, ushort dataKey, uint iconId) {
            var snapshot = new MapMarkerInfo { DataType = dataType, DataKey = dataKey };
            snapshot.MapMarker.IconId = iconId;
            OnClicked = () => OnMarkerClicked(ref snapshot);
            Secondary = () => GetTooltip(ref snapshot);
            FallbackPrimary = dataType switch {
                4 => () => Service.DataManager.GetExcelSheet<PlaceName>().GetRow(snapshot.DataKey).Name.ExtractText(),
                _ => () => System.TooltipCache.GetValue(snapshot.MapMarker.IconId) ?? string.Empty,
            };
        }
    }

    private static readonly global::System.Collections.Generic.Dictionary<(ushort Type, ushort Key, uint Icon), NativeCallbacks>
        Callbacks = [];

    private static readonly Func<string?> EmptyText = static () => string.Empty;

    private static CachedText CachedSubtext(InteropGenerator.Runtime.CStringPointer text) {
        var bytes = text.AsSpan();
        var hash = new HashCode();
        hash.AddBytes(bytes);
        var key = (hash.ToHashCode(), bytes.Length);
        if (Subtexts.TryGetValue(key, out var cached))
            return cached;
        if (Subtexts.Count >= 1024)
            Subtexts.Clear();
        return Subtexts[key] = new CachedText(text.AsDalamudSeString());
    }

    private static NativeCallbacks CachedCallbacks(MapMarkerInfo marker) {
        var key = (Type: marker.DataType, Key: marker.DataKey, Icon: marker.MapMarker.IconId);
        if (Callbacks.TryGetValue(key, out var cached))
            return cached;
        if (Callbacks.Count >= 1024)
            Callbacks.Clear();
        return Callbacks[key] = new NativeCallbacks(key.Type, key.Key, key.Icon);
    }

    private static float TextOpacity(MapMarkerInfo marker) => marker.MapMarker.SubtextStyle is 1
        ? System.SystemConfig.LargeAreaTextOpacity : System.SystemConfig.SmallAreaTextOpacity;

    // A label that TryDrawText would drop anyway; checked up front so no marker object is built for it.
    private static bool IsTextHidden(MapMarkerInfo marker) {
        if (!System.SystemConfig.ShowTextLabels)
            return true;
        var opacity = TextOpacity(marker);
        return Rendering.MapDrawContext.Current.Config.IsGratis && float.IsFinite(opacity) && opacity <= .01f;
    }

    public static void Draw(this MapMarkerInfo marker, Vector2 offset, float scale) {
        var isText = marker.MapMarker.IconId is 0 && marker.MapMarker.Index is not 0;
        // DrawMapMarker ignores icon 0 and TryDrawText drops hidden labels: neither needs a marker object built.
        if (marker.MapMarker.IconId is 0 && !isText)
            return;
        if (isText && IsTextHidden(marker))
            return;

        var tooltipText = CachedSubtext(marker.MapMarker.Subtext);
        var callbacks = CachedCallbacks(marker);

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
            PrimaryText = GetMarkerPrimaryTooltip(marker, tooltipText, callbacks),
            OnLeftClicked = Rendering.MapDrawContext.Current.Config.IsGratis && marker.DataType is 3 or 4 ? null
                : callbacks.OnClicked,
            SecondaryText = callbacks.Secondary,
        };

        if (isText) {
            TryDrawText(marker, markerInfo, tooltipText);
        }
        else {
            DrawHelpers.DrawMapMarker(markerInfo);
        }
    }

    private static void TryDrawText(MapMarkerInfo marker, MarkerInfo markerInfo, CachedText tooltipText) {
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

        var opacity = TextOpacity(marker);
        var fade = Rendering.MapDrawContext.Current.Config.IsGratis && float.IsFinite(opacity);
        if (fade && opacity <= .01f)
            return;
        if (fade)
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * Math.Clamp(opacity, 0, 1));
        DrawHelpers.DrawText(markerInfo, tooltipText.Text);
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

    private static Func<string?> GetMarkerPrimaryTooltip(MapMarkerInfo marker, CachedText tooltipText,
        NativeCallbacks callbacks) {
        if (DrawHelpers.IsDisallowedIcon(marker.MapMarker.IconId))
            return EmptyText;
        if (!System.SystemConfig.ShowMiscTooltips)
            return EmptyText;
        if (!tooltipText.IsEmpty)
            return tooltipText.Tooltip;

        return callbacks.FallbackPrimary;
    }
}