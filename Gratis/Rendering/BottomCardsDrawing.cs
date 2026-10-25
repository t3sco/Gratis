using System;
using System.Globalization;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public readonly record struct MapLocation(string Region, string Zone, string Area)
{
    public static MapLocation FromTitle(string title, SubZoneLocation? sub = null)
    {
        title = title.Split("###")[0];
        var parts = title.Split(" - ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 1 && parts[0] == "Eorzea") parts = parts[1..];
        var region = parts.Length > 1 ? parts[0] : "";
        var zone = sub?.MapName ?? (parts.Length > 1 ? parts[1] : parts.Length == 1 ? parts[0] : "");
        var area = sub is not null ? string.IsNullOrWhiteSpace(sub.SubAreaName) ? sub.AreaName : sub.SubAreaName : parts.Length > 2 ? parts[^1] : "";
        return new(region, zone, area);
    }
    public (string Secondary, string Primary) Lines(LocationCardContent mode)
    {
        var primary = string.IsNullOrWhiteSpace(Area) ? Zone : Area;
        return mode switch {
            LocationCardContent.AreaOnly => ("", primary),
            LocationCardContent.Zone => ("", Zone),
            LocationCardContent.RegionZoneArea => (Join(Region, primary == Zone ? "" : Zone), primary),
            _ => (primary == Zone ? Region : Zone, primary),
        };
    }
    private static string Join(string a, string b) => a.Length == 0 ? b : b.Length == 0 || a == b ? a : a + " · " + b;
}

public static class BottomCardsDrawing
{
    public const float Height = 56;
    private static readonly string[] Digits = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];
    private static MapDrawContext? context;
    private static MapLocation location;
    private static Vector4 locationBounds, coordinatesBounds;
    private static int frame = -1;
    private static float coordinateWidth;
    public static string LocationPrimary { get; private set; } = "";
    public static string LocationSecondary { get; private set; } = "";
    public static float DigitAdvance { get; private set; }
    public static void Prepare(string title, SubZoneLocation? sub = null, MapLocation? place = null)
    {
        if (!ReferenceEquals(context, MapDrawContext.Current)) { context = MapDrawContext.Current; frame = -1; }
        var next = place ?? MapLocation.FromTitle(title, sub);
        if (next != location) frame = -1;
        location = next;
    }
    private static void Layout(float requestedCoordinates)
    {
        var current = MapDrawContext.Current; var c = current.Config; var s = GratisTheme.ChromeScale;
        if (!ReferenceEquals(context, current)) { context = current; location = default; frame = -1; }
        if (frame == ImGui.GetFrameCount() && MathF.Abs(coordinateWidth - requestedCoordinates) < .1f) return;
        frame = ImGui.GetFrameCount(); coordinateWidth = requestedCoordinates;
        var (min, max) = ChromeLayout.SafeBounds(); var available = max.X - min.X;
        locationBounds = coordinatesBounds = Vector4.Zero;
        var safeLocation = new MapLocation(location.Region ?? "", location.Zone ?? "", location.Area ?? "");
        (LocationSecondary, LocationPrimary) = safeLocation.Lines(c.LocationCardShows);
        var showLocation = c.ShowTitleOnMap && c.CompactTitle && LocationPrimary.Length > 0;
        float titleWidth;
        using (current.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Title)?.Push()) titleWidth = ImGui.CalcTextSize(LocationPrimary).X;
        using (current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push()) titleWidth = MathF.Max(titleWidth, ImGui.CalcTextSize(LocationSecondary).X);
        titleWidth = Math.Clamp(titleWidth + 24 * s, 96 * s, 280 * s);
        var coordWidth = MathF.Min(requestedCoordinates, available);
        if (showLocation && requestedCoordinates > 0 && c.LocationAnchor == c.CoordinateAnchor) {
            titleWidth = MathF.Min(titleWidth, available - coordWidth - GratisTheme.Gap * s);
            if (titleWidth >= 64 * s) {
                var pair = ChromeLayout.Place("bottom-cards", new(titleWidth + coordWidth + GratisTheme.Gap * s, Height * s), c.LocationAnchor);
                if (pair != Vector4.Zero) {
                    locationBounds = new(pair.X, pair.Y, pair.X + titleWidth, pair.W);
                    coordinatesBounds = new(locationBounds.Z + GratisTheme.Gap * s, pair.Y, pair.Z, pair.W);
                    return;
                }
            }
        }
        if (requestedCoordinates > 0) coordinatesBounds = ChromeLayout.Place("coordinates", new(coordWidth, Height * s), c.CoordinateAnchor);
        if (showLocation) locationBounds = ChromeLayout.Place("location", new(MathF.Min(titleWidth, available), Height * s), c.LocationAnchor);
    }
    public static void DrawLocation(string title)
    {
        if (!ReferenceEquals(context, MapDrawContext.Current) || location.Zone is null) Prepare(title);
        Layout(MapDrawContext.Current.Config.ShowCoordinateBar ? coordinateWidth : 0);
        if (locationBounds == Vector4.Zero) return;
        var c = MapDrawContext.Current; var s = GratisTheme.ChromeScale;
        var pos = ChromeLayout.Position(locationBounds); var size = new Vector2(locationBounds.Z - pos.X, Height * s);
        MapWindowDrawing.RecordBounds(pos, pos + size, true);
        var list = ImGui.GetWindowDrawList();
        MapWindowDrawing.DrawSurface(list, pos, pos + size, GratisTheme.CardRadius * s, surface: GlassSurface.Title);
        using (c.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push())
            list.AddText(pos + new Vector2(12, 8) * s, ImGui.GetColorU32(GratisTheme.Secondary), ChromeControls.FitLabel(LocationSecondary, size.X - 24 * s));
        using (c.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Title)?.Push())
            list.AddText(pos + new Vector2(12 * s, LocationSecondary.Length > 0 ? 30 * s : (size.Y - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(GratisTheme.Primary), ChromeControls.FitLabel(LocationPrimary, size.X - 24 * s));
        ImGui.SetCursorScreenPos(pos); ImGui.Dummy(size);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(LocationSecondary.Length > 0 ? LocationSecondary + " · " + LocationPrimary : LocationPrimary);
    }
    public static void DrawCoordinates(Vector2? player, Vector2? cursor)
    {
        var c = MapDrawContext.Current; var s = GratisTheme.ChromeScale;
        using var alpha = new ChromeVisibility.AlphaScope(true, c.Chrome.ControlsAlpha(c.Config));
        if (!alpha.Visible) return;
        using var font = c.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push();
        DigitAdvance = 0;
        foreach (var digit in "0123456789-./") DigitAdvance = MathF.Max(DigitAdvance, ImGui.CalcTextSize(digit.ToString()).X);
        DigitAdvance = MathF.Ceiling(DigitAdvance);
        var valueWidth = 15 * DigitAdvance;
        var width = valueWidth + 46 * s;
        var (safeMin, safeMax) = ChromeLayout.SafeBounds();
        var time = c.Config.ShowEorzeaTime ? EorzeaTime.Format(c.UnixTimeSeconds) : "";
        var timeWidth = time.Length > 0 ? ImGui.CalcTextSize(time).X + 16 * s : 0;
        if (width + timeWidth + 104 * s < safeMax.X - safeMin.X) width += timeWidth; else time = "";
        Layout(width);
        if (coordinatesBounds == Vector4.Zero) return;
        var pos = ChromeLayout.Position(coordinatesBounds); var size = new Vector2(coordinatesBounds.Z - pos.X, Height * s);
        MapWindowDrawing.RecordBounds(pos, pos + size, false);
        var list = ImGui.GetWindowDrawList();
        MapWindowDrawing.DrawSurface(list, pos, pos + size, GratisTheme.CardRadius * s, surface: GlassSurface.Coordinates);
        var mode = Enum.IsDefined(c.Config.CoordinatesShow) ? c.Config.CoordinatesShow : CoordinateContent.Both;
        var both = mode == CoordinateContent.Both;
        Vector4 playerBounds = Vector4.Zero, cursorBounds = Vector4.Zero;
        void Row(Vector2? value, bool isPlayer, float y)
        {
            var start = pos + new Vector2(12 * s, y);
            var ink = value.HasValue ? GratisTheme.Primary : GratisTheme.Secondary with { W = .45f };
            var icon = start + new Vector2(7 * s, ImGui.GetFontSize() / 2);
            if (isPlayer) {
                list.AddCircle(icon - new Vector2(0, 3 * s), 3 * s, ImGui.GetColorU32(ink), 16, s);
                list.AddLine(icon + new Vector2(-5, 6) * s, icon + new Vector2(-5, 3) * s, ImGui.GetColorU32(ink), s);
                list.AddLine(icon + new Vector2(5, 6) * s, icon + new Vector2(5, 3) * s, ImGui.GetColorU32(ink), s);
                list.AddLine(icon + new Vector2(-5, 3) * s, icon + new Vector2(5, 3) * s, ImGui.GetColorU32(ink), s);
            }
            else GratisIcons.Draw(list, icon, GratisIcons.Crosshair, ink, GratisTheme.IconSize * s);
            var textPos = start + new Vector2(22 * s, 0);
            var text = value is { } point ? Axis(point.X) + " / " + Axis(point.Y) : "  --.- /   --.-";
            DrawFixed(list, textPos, text, ImGui.GetColorU32(ink));
            var bounds = new Vector4(start.X, start.Y - 2 * s, textPos.X + valueWidth, start.Y + 18 * s);
            if (isPlayer) playerBounds = bounds; else cursorBounds = bounds;
        }
        if (mode != CoordinateContent.Cursor) Row(player, true, both ? 8 * s : (size.Y - ImGui.GetFontSize()) / 2);
        if (mode != CoordinateContent.Player) Row(cursor, false, both ? 30 * s : (size.Y - ImGui.GetFontSize()) / 2);
        CoordinateCopy.RecordRows(playerBounds, cursorBounds);
        if (time.Length > 0) list.AddText(pos + new Vector2(size.X - timeWidth + 4 * s, (size.Y - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(GratisTheme.Secondary), time);
        ImGui.SetCursorScreenPos(pos); ImGui.Dummy(size);
    }
    private static string Axis(float n) => n.ToString("0.0", CultureInfo.InvariantCulture).PadLeft(6);
    public static void DrawFixed(ImDrawListPtr list, Vector2 pos, string text, uint ink)
    {
        foreach (var ch in text) {
            var glyph = ch is >= '0' and <= '9' ? Digits[ch - '0'] : ch switch { '-' => "-", '.' => ".", '/' => "/", _ => " " };
            var width = ImGui.CalcTextSize(glyph).X;
            if (ch != ' ') list.AddText(new(MathF.Round(pos.X + (DigitAdvance - width) / 2), MathF.Round(pos.Y)), ink, glyph);
            pos.X += DigitAdvance;
        }
    }
}
