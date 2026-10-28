using System.Text.Json;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;

namespace Gratis.Preview;

internal static class PlaceChipVerification {
    private static void Check(bool value, string name) {
        if (!value)
            throw new InvalidOperationException("Place chips: " + name);
    }

    internal static SubZoneLocation Location(uint sub = 2) =>
        new(1, 1, sub, "Lower La Noscea", "Cedarwood", sub == 2 ? "The Gods' Grip" : "Moraby Drydocks");
    public static void State() {
        foreach (var (unix, expected) in new (long, string)[] { (0, "ET 00:00"), (1, "ET 00:00"), (35, "ET 00:12"),
            (174, "ET 00:59"), (175, "ET 01:00"), (2100, "ET 12:00"), (2544, "ET 14:32"), (4199, "ET 23:59"),
            (4200, "ET 00:00"), (-1, "ET 23:59"), (-4200, "ET 00:00") })
            Check(EorzeaTime.Format(unix) == expected, "known conversion " + unix);
        Check(EorzeaTime.MinuteOfDay(long.MinValue) is >= 0 and < 1440
            && EorzeaTime.MinuteOfDay(long.MaxValue) is >= 0 and < 1440,
            "Int64 boundaries");
        for (var second = 0; second < 4200; second++)
            Check(EorzeaTime.MinuteOfDay(second) == EorzeaTime.MinuteOfDay(second + 4200), "day periodicity");
        var c = JsonSerializer.Deserialize<SystemConfig>("{}", ScriptedFrameDriver.Json)!;
        Check(c.SubZoneBanner && !c.ShowEorzeaTime, "old-config defaults");
        var b = new SubZoneBanner();
        b.Update(c, Location(), 0);
        Check(b.Opacity == 0, "initial baseline");
        b.Update(c, Location(3), 1);
        b.Update(c, Location(3), 1.2);
        Check(b.Text == "Moraby Drydocks" && b.Zone == "Lower La Noscea" && b.Opacity == 1, "sub-area change");
        b.Update(c, Location(3), 2.65);
        Check(b.Opacity > 0 && b.Opacity < 1, "fade out");
        b.Update(c, Location(3), 2.9);
        Check(b.Opacity == 0, "1.8s expiry");
        b.Update(c, Location(3) with { MapId = 2 }, 3);
        Check(b.Opacity == 0, "map changes suppressed");
        b.Update(c, Location(3) with { MapId = 2, AreaPlaceNameId = 5, SubAreaPlaceNameId = 0, SubAreaName = "" }, 3.2);
        b.Update(c, b.Location, 3.4);
        Check(b.Text.EndsWith("Cedarwood") && b.Opacity == 1, "area fallback");
        c.SubZoneBanner = false;
        b.Update(c, Location(3), 4);
        Check(b.Opacity == 0, "disabled");
        c.SubZoneBanner = true;
        c.VisualStyle = 1;
        b.Update(c, Location(), 5);
        Check(b.Opacity == 0, "Classic unchanged");
        c.VisualStyle = 0;
        b.Update(c, null, 6);
        b.Update(c, Location(), 7);
        Check(b.Opacity == 0, "unknown location resets baseline");
        c.ShowEorzeaTime = true;
        foreach (var path in new[] {
            "SubZoneBanner",
            "ShowEorzeaTime" })
            SettingsLayout.Entries.Single(e => e.Path == path).Reset(c, new());
        Check(c.SubZoneBanner && !c.ShowEorzeaTime, "catalog resets");
    }

    internal static SystemConfig Config() => new() {
        AcceptedSpoilerWarning = true, FadeMode = 0, ChromeIdleDelay = 1.2f, ChromeFadeDurationMs = 180,
        WindowPosition = new(32), WindowSize = new(640, 480), FollowPlayer = false
    };
    public static void Frames() {
        using var d = new ScriptedFrameDriver(Config(), new FrameScript { SubZone = Location() });
        d.Step(new(0, Focused: false));
        d.Step(new(2));
        d.Step(new(2.1, SubZone: Location(3)));
        var banner = d.Step(new(2.3));
        Check(banner.ChromeProgress == 0 && banner.SubZoneOpacity == 1 && banner.SubZoneText.Contains("Moraby"),
            "banner overrides hidden chrome");
        var expired = d.Step(new(4));
        Check(expired.SubZoneOpacity == 0, "shared-frame expiry");
        d.Config.PinChrome = true;
        var without = d.Step(new(4.1));
        d.Config.ShowEorzeaTime = true;
        var clock = d.Step(new(4.2, UnixTime: 2544));
        Check(clock.EorzeaText == "ET 14:32" && clock.CoordinatesBlur != "not-drawn", "ET shared coordinate surface");
        d.Config.StyledCoordinates = false;
        var plain = d.Step(new(4.3, UnixTime: 2100));
        Check(plain.EorzeaText == "ET 12:00", "legacy coordinate choice uses ET chip when enabled");
        var other = d.Step(new(4.4, MapId: 2));
        Check(other.SubZoneOpacity == 0, "different selected map suppresses banner");
        d.Config.VisualStyle = 1;
        Check(d.Step(new(4.5)).EorzeaText == "", "Classic ignores ET");
    }
}