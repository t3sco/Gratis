using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;

namespace Gratis.Preview;

internal static class SettingsVerification {
    public static void Run() {
        Program.VerifyResponsiveSettings();
        if (!SettingsLayout.Entries.Single(e => e.Path == "CoordinatesShow").Matches("coordinates show")
            || !SettingsLayout.Entries.Single(e => e.Path == "LocationCardShows").Matches("location card shows")
            || !SettingsLayout.Entries.Single(e => e.Path == "PinChrome").Matches("keep controls visible"))
            throw new InvalidOperationException("Search missed the labels shown in settings.");
        var legacyConfig = global::System.Text.Json.JsonSerializer.Deserialize<SystemConfig>(
            "{\"VisualStyle\":1,\"TitleScale\":1.8,\"MapGlowSize\":37,\"UnknownFormerSetting\":true}",
            new global::System.Text.Json.JsonSerializerOptions { IncludeFields = true })!;
        if (legacyConfig.IsGratis || legacyConfig.TitleScale != 1.8f || legacyConfig.MapGlowSize != 37)
            throw new InvalidOperationException("Legacy configuration values did not survive loading.");
        static double Luminance(Vector4 color) {
            static double Linear(float channel) => channel <= .04045 ? channel / 12.92
                : Math.Pow((channel + .055) / 1.055, 2.4);
            return .2126 * Linear(color.X) + .7152 * Linear(color.Y) + .0722 * Linear(color.Z);
        }
        var background = Vector4.Lerp(GratisTheme.Surface with { W = 1 }, Vector4.One, .08f);
        var dimmed = Vector4.Lerp(background, GratisTheme.Primary, .7f);
        foreach (var text in new[] { GratisTheme.Primary, GratisTheme.Secondary, dimmed })
            if ((Luminance(text) + .05) / (Luminance(background) + .05) < 4.5)
                throw new InvalidOperationException("Settings text contrast fell below 4.5:1.");
        var config = new SystemConfig { MapGlowSize = 37, TitleScale = 1.8f, CoordinateBarFade = .12f };
        var defaults = new SystemConfig();
        var legacy = SettingsLayout.Entries.Where(entry =>
            entry.Group.StartsWith("Classic", StringComparison.Ordinal)).ToArray();
        if (legacy.Length != 37 || legacy.Any(entry => entry.Visible(config)))
            throw new InvalidOperationException("Classic controls leaked into Gratis.");
        config.VisualStyle = 1;
        if (legacy.Any(entry => !entry.Visible(config)))
            throw new InvalidOperationException("Classic controls are inaccessible in Classic.");
        if (SettingsLayout.Find(config, "Chrome & motion", "title colour").All(entry => entry.Path != "TitleColor"))
            throw new InvalidOperationException("Search missed a Classic setting by name.");
        config.VisualStyle = 0;
        if (SettingsLayout.Find(config, "Chrome & motion", "title colour").Length != 0)
            throw new InvalidOperationException("Search exposed Classic styling in Gratis.");
        if (SettingsLayout.Find(config, "Map", "zoom").All(entry => entry.Path != "ZoomSpeed"))
            throw new InvalidOperationException("Search missed wheel zoom by group/name.");
        if (!SettingsLayout.MarkerOptionsMatch("cluster distance") || SettingsLayout.MarkerOptionsMatch("glass"))
            throw new InvalidOperationException("Marker extension search routing failed.");
        config.AccentColor = Vector4.Zero;
        config.MapColour.Preset = MapColourPreset.Sepia;
        SettingsLayout.Reset(config, "Appearance");
        if (config.AccentColor != defaults.AccentColor || config.MapColour.Preset != MapColourPreset.Original
            || config.MapGlowSize != 37)
            throw new InvalidOperationException(
                "Appearance reset damaged hidden Classic values or missed visible settings.");
        config.Motion.EaseZoom = false;
        config.ChromeIdleDelay = 9;
        SettingsLayout.Reset(config, "Chrome & motion");
        if (!config.Motion.EaseZoom || config.ChromeIdleDelay != defaults.ChromeIdleDelay || config.TitleScale != 1.8f
            || config.CoordinateBarFade != .12f)
            throw new InvalidOperationException("Chrome reset damaged Classic values or omitted motion.");
        config.Markers.Declutter = !defaults.Markers.Declutter;
        config.Markers.IconStyle = MarkerIconStyle.Game;
        SettingsLayout.Reset(config, "Markers");
        if (config.Markers.Declutter != defaults.Markers.Declutter
            || config.Markers.IconStyle != defaults.Markers.IconStyle)
            throw new InvalidOperationException("Markers reset omitted the owner settings.");
        config.WindowPosition = new Vector2(123, 456);
        SettingsLayout.Reset(config, "Map");
        if (config.WindowPosition != new Vector2(123, 456))
            throw new InvalidOperationException("Map reset moved the window.");
        config.NeutralFrame = false;
        config.FramePreset = FramePreset.Hairline;
        if (SettingsLayout.Find(config, "Appearance", "ring").All(entry => entry.Path != "RingEnabled"))
            throw new InvalidOperationException("Live compatibility ring controls are inaccessible.");
        config.VisualStyle = 1;
        SettingsLayout.Reset(config, "Chrome & motion");
        if (config.TitleScale != defaults.TitleScale || config.CoordinateBarFade != defaults.CoordinateBarFade)
            throw new InvalidOperationException("Classic styling did not reset.");
        Console.WriteLine($"Settings verified: {SettingsLayout.Entries.Length} shared controls, 37 Classic "
            + "controls disclosed, name search, owner routing, conditional ring access and section resets preserve "
            + "unrelated/hidden settings.");
    }
}