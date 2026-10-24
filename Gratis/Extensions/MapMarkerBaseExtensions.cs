using System.Drawing;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Classes;

namespace Gratis.Extensions;

public static class MapMarkerBaseExtensions
{
    public static void Draw(this MapMarkerBase marker, Vector2 offset, float scale)
    {
        var tooltipText = marker.Subtext.AsDalamudSeString();

        DrawHelpers.DrawMapMarker(new MarkerInfo
        {
            Position = new Vector2(marker.X, marker.Y) / 16.0f * scale + DrawHelpers.GetMapCenterOffsetVector() * scale,
            Offset = offset,
            Scale = scale,
            Radius = marker.Scale,
            RadiusColor = KnownColor.MediumPurple.Vector(),
            IconId = marker.IconId,
            PrimaryText =
                () => tooltipText.TextValue.IsNullOrEmpty() && System.SystemConfig.ShowMiscTooltips ? System.TooltipCache.GetValue(marker.IconId) : tooltipText.ToString(),
        });
    }
}