using System;
using System.Collections.Generic;
using System.Text.Json;
using Gratis.Data;
using Gratis.Windows;

namespace Gratis.Rendering;

/// <summary>A built-in look: a code-built profile plus the numbers its card swatch draws from.</summary>
public sealed record QuickSetupLook(string Name, string Blurb, string Data, ProfileData Profile, float Radius,
    bool Glass, bool Classic);

public static class QuickSetupLooks {
    public const int Vanilla = 0, Glass = 1, Classic = 2;
    // Every setting a look may change. Applying one resets these first, so looks never stack and the rest is untouched.
    private static readonly string[] Owned = ["VisualStyle", "FlatCornerRadius", "GlassEnabled", "GlassBlur",
        "AccentColor", "FramePreset", "NeutralFrame"];
    private static QuickSetupLook[]? built;

    public static IReadOnlyList<QuickSetupLook> All => built ??= Build();

    private static QuickSetupLook[] Build() {
        var defaults = new SystemConfig();
        var accent = defaults.AccentColor;
        var glass = new Dictionary<string, JsonElement> {
            ["FlatCornerRadius"] = Element(48f),
            ["GlassEnabled"] = Element(true),
            ["GlassBlur"] = Element(true),
            ["AccentColor"] = Element(new[] { accent.X, accent.Y, accent.Z, accent.W }),
            ["FramePreset"] = Element((int)FramePreset.Hairline),
            ["NeutralFrame"] = Element(true),
        };
        return [
            Make("Vanilla+", "The defaults: lightly rounded corners and quiet frosted panels.", new(),
                defaults.FlatCornerRadius, defaults.GlassEnabled, false),
            Make("Gratis glass", "Rounder corners and frosted glass panels, with a teal accent.", glass, 48, true,
                false),
            Make("Classic", "Mappy's layout: plain window and toolbar. Gratis features are not available.",
                new() { ["VisualStyle"] = Element(1) }, defaults.MapRounding, false, true),
        ];
    }

    private static JsonElement Element<T>(T value) => JsonSerializer.SerializeToElement(value);

    private static QuickSetupLook Make(string name, string blurb, Dictionary<string, JsonElement> values, float radius,
        bool glass, bool classic) {
        var data = ProfileCodec.Encode(name, values);
        return ProfileCodec.TryDecode(data, out var profile, out _)
            ? new(name, blurb, data, profile!, radius, glass, classic)
            : throw new InvalidOperationException("Built-in look " + name + " does not decode.");
    }

    /// <summary>Which look the current settings match. A custom corner radius counts as Vanilla+.</summary>
    public static int Current(SystemConfig config) => !config.IsGratis ? Classic
        : MathF.Abs(config.FlatCornerRadius - 48) < 1 ? Glass : Vanilla;

    /// <summary>Applies a look through the profile codec, keeping every setting the looks do not own.</summary>
    public static int Apply(SystemConfig config, int index) {
        var look = All[index];
        var merged = ProfileCodec.Capture(config);
        foreach (var key in Owned)
            merged.Remove(key);
        foreach (var pair in look.Profile.Values)
            merged[pair.Key] = pair.Value;
        return ProfileCodec.Apply(config, new(look.Name, merged));
    }
}
