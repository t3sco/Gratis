using System.Collections.Generic;
using System.Linq;
using Gratis.Data;
using Gratis.Rendering;

namespace Gratis.Windows;

/// <summary>
/// The preset controls, as data: each choice is the raw catalog settings it sets, matched with a small tolerance on
/// numbers. Order matters only for display: no two choices of a preset match the same config.
/// </summary>
public static class SettingsPresets {
    private static Dictionary<string, object> With(Dictionary<string, object> basis, params (string, object)[] changes) {
        var result = new Dictionary<string, object>(basis);
        foreach (var (path, value) in changes)
            result[path] = value;
        return result;
    }

    private static readonly Dictionary<string, object> MotionFull = new() {
        ["InterfaceAnimations"] = InterfaceAnimationMode.Full, ["AnimationSpeed"] = 1f, ["ReduceMotion"] = false,
        ["Motion.Pings"] = true, ["Motion.PingSeconds"] = .9f, ["Motion.PlayerTrail"] = true,
        ["Motion.TrailSeconds"] = 8.3f, ["Motion.TrailOpacity"] = .33f, ["Motion.EaseFollow"] = true,
        ["Motion.EaseZoom"] = true, ["Motion.PanInertia"] = true, ["Motion.PanInertiaStrength"] = .35f,
        ["Motion.SettleSeconds"] = .12f, ["Motion.ZoneTransition"] = true, ["Motion.ZoneSeconds"] = .18f,
        ["Motion.ZoneSlidePixels"] = 8f, ["PlayerArrowSmoothing"] = PlayerArrowSmoothing.Low,
    };

    private static readonly Dictionary<string, object> GlassFrosted = new() {
        ["GlassEnabled"] = true, ["GlassBlur"] = true, ["GlassBlurRadius"] = 4f, ["GlassTintOpacity"] = 0f,
        ["GlassHighlight"] = .21f, ["GlassGrain"] = false,
    };

    private static readonly Dictionary<string, object> LabelsNormal = new() {
        ["ShowTextLabels"] = true, ["LargeAreaTextScale"] = 1f, ["SmallAreaTextScale"] = .5f,
        ["LargeAreaTextOpacity"] = .3f, ["SmallAreaTextOpacity"] = 1f,
    };

    public static readonly SettingsPresetDefinition[] Definitions = [
        new("motion", "Chrome & motion", "Motion", "Motion", "How much the map and controls animate.", [
            new("Off", new() {
                ["ReduceMotion"] = true, ["InterfaceAnimations"] = InterfaceAnimationMode.Off,
                ["PlayerArrowSmoothing"] = PlayerArrowSmoothing.Off }),
            new("Calm", With(MotionFull, ("AnimationSpeed", .75f), ("Motion.PlayerTrail", false),
                ("Motion.PanInertia", false), ("Motion.ZoneSlidePixels", 0f), ("Motion.PingSeconds", .6f))),
            new("Full", MotionFull),
        ], Keep: ["PlayerArrowSmoothing"]),

        new("controls-visibility", "Chrome & motion", "Chrome visibility", "Controls visibility",
            "When the toolbar and cards show.", [
            new("Always shown", new() { ["AutoHideChrome"] = false, ["PinChrome"] = false }),
            new("Auto-hide", new() { ["AutoHideChrome"] = true, ["PinChrome"] = false }),
            new("Pinned", new() { ["AutoHideChrome"] = false, ["PinChrome"] = true }),
        ]),

        new("map-fade", "Chrome & motion", "Map opacity", "Map fade", "Dim the map, or click through it.", [
            new("Solid", new() { ["FadeMode"] = (FadeMode)0, ["GhostMode"] = false }),
            new("Dim", new() {
                ["FadeMode"] = FadeMode.WhenUnFocused, ["FadePercent"] = .81f, ["GhostMode"] = false },
                "Dim when unfocused"),
            new("Click-through", new() {
                ["GhostMode"] = true, ["GhostOpacity"] = .13f, ["GhostRestoreDelay"] = 1.6f,
                ["RestoreOpacityOnHover"] = true }, "Click-through while moving"),
        ], Keep: ["FadeMode"]),

        new("frame-effects", "Appearance", "Frame effects", "Frame effects", "Frame and corner looks in one click.", [
            new("Quiet", new() {
                ["NeutralFrame"] = true, ["FramePreset"] = FramePreset.Hairline, ["FeatherEnabled"] = false }),
            new("Soft", new() {
                ["NeutralFrame"] = true, ["FramePreset"] = FramePreset.SoftShadow, ["FlatCornerRadius"] = 24f,
                ["FeatherEnabled"] = true, ["FeatherWidth"] = 25f }),
            new("Glow", new() {
                ["NeutralFrame"] = true, ["FramePreset"] = FramePreset.InsetGlow, ["FlatCornerRadius"] = 16f }),
            new("Etched", new() {
                ["NeutralFrame"] = true, ["FramePreset"] = FramePreset.DoubleRing, ["FlatCornerRadius"] = 8f,
                ["FeatherEnabled"] = false }),
        ], Keep: ["NeutralFrame", "FramePreset", "FlatCornerRadius", "FeatherEnabled"]),

        new("glass", "Appearance", "Glass surfaces", "Glass", "How frosted the panels look.", [
            new("Off", new() { ["GlassEnabled"] = false }),
            new("Light", new() {
                ["GlassEnabled"] = true, ["GlassBlur"] = false, ["GlassTintOpacity"] = .02f, ["GlassHighlight"] = .21f,
                ["GlassGrain"] = false }),
            new("Frosted", GlassFrosted),
            new("Frosted + grain", With(GlassFrosted, ("GlassGrain", true), ("GlassGrainIntensity", .028f),
                ("GlassBlurRadius", 12f))),
        ]),

        new("map-labels", "Map", "Map labels and fog", "Map labels", "How loud the map's place names are.", [
            new("Off", new() { ["ShowTextLabels"] = false }),
            new("Quiet", new() {
                ["ShowTextLabels"] = true, ["LargeAreaTextOpacity"] = .15f, ["SmallAreaTextOpacity"] = .7f,
                ["HideOverlappingLabels"] = true }),
            new("Normal", LabelsNormal),
            new("Large", With(LabelsNormal, ("LargeAreaTextScale", 1.5f), ("SmallAreaTextScale", .75f))),
        ]),

        new("spotlight", "Map", "Spotlight", "Spotlight", "How strongly quest targets stand out.", [
            new("Off", new() { ["QuestSpotlight"] = false }),
            new("Subtle", new() {
                ["QuestSpotlight"] = true, ["SpotlightDim"] = .15f, ["SpotlightPulse"] = false,
                ["SpotlightSeconds"] = 4f, ["SpotlightGuideLine"] = false, ["SpotlightHideOthers"] = false }),
            new("Full", new() {
                ["QuestSpotlight"] = true, ["SpotlightDim"] = .35f, ["SpotlightPulse"] = true,
                ["SpotlightSeconds"] = 8f, ["SpotlightGuideLine"] = true, ["SpotlightHideOthers"] = true }),
        ]),

        new("follow-return", "Map", "Viewport", "After I drag the map", "Whether the map comes back to you.", [
            new("Stay", new() { ["FollowReturnToPlayer"] = false }, "Stay where I dragged it"),
            new("Return when I move", new() {
                ["FollowReturnToPlayer"] = true, ["FollowReturnTrigger"] = FollowReturnTrigger.WhenPlayerMoves }),
            new("Return after idle", new() {
                ["FollowReturnToPlayer"] = true, ["FollowReturnTrigger"] = FollowReturnTrigger.AfterIdle,
                ["FollowReturnIdleSeconds"] = 3f }),
        ]),

        new("map-open", "Map", "On open", "When the map opens", "Where the map points when it opens.", [
            new("Follow me", new() { ["FollowOnOpen"] = true, ["CenterOnOpen"] = CenterTarget.Player }),
            new("Centre the map", new() { ["FollowOnOpen"] = false, ["CenterOnOpen"] = CenterTarget.Map }),
            new("Stay as I left it", new() {
                ["FollowOnOpen"] = false, ["CenterOnOpen"] = CenterTarget.Disabled, ["RememberMapView"] = true },
                "Reopen each map as you left it"),
        ], Keep: ["RememberMapView"]),

        new("quest-log-opens", "Map", "Quest Log", "Quest log opens", "How the Quest Log shows when the map opens.", [
            new("Hidden", new() { ["QuestLogOpenWithMap"] = false, ["QuestLogPinned"] = false }),
            new("With map", new() { ["QuestLogOpenWithMap"] = true, ["QuestLogPinned"] = false },
                "Opens with the map; you can close it"),
            new("Pinned", new() { ["QuestLogPinned"] = true }, "Stays open until you unpin it"),
        ]),
    ];

    /// <summary>Settings a preset sets that Show advanced hides: everything it sets except its Keep list.</summary>
    internal static HashSet<string> CoveredPaths() => [.. Definitions.SelectMany(definition =>
        definition.Choices.SelectMany(choice => choice.Values.Keys).Except(definition.Keep ?? []))];
}
