using System;
using System.Collections.Generic;
using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Windows;

public sealed partial class SettingsLayout {
    private sealed record EssentialCard(string Id, string Title, char Icon, string[] Paths, string[]? PresetIds = null,
        string[]? MultiIds = null) {
        public string HeaderId { get; } = "##essentials-" + Id;
        public string RowsId { get; } = "essentials-rows-" + Id;
    }

    // The ten settings a new user decides in the first minute (audit 6.3), as cards in the order they are listed.
    // Rows are the catalog's own entries, so search, profiles and resets keep treating them as before. VisualStyle is
    // drawn as the three-look selector, which also covers the Classic switch.
    private static readonly EssentialCard[] EssentialDefinitions = [
        new("look", "Look", GratisIcons.Eye, ["VisualStyle", "AccentColor", "MapCircle", "FlatCornerRadius"]),
        new("controls", "Controls and motion", GratisIcons.Layers, ["FollowPlayer"],
            ["controls-visibility", "map-fade", "motion"]),
        new("features", "Features", GratisIcons.Compass, ["TravelActions", "QuestLogReplaceJournal"],
            ["quest-log-opens"]),
        new("window", "Window", GratisIcons.EyeOff, [], MultiIds: ["hide-map"]),
    ];
    private static (EssentialCard Card, SettingsEntry[] Entries, SettingsPreset[] Presets,
        SettingsMultiSelect[] MultiSelects)[]? essentialCards;
    private readonly List<SettingsEntry> essentialRows = [];
    private readonly List<SettingsPreset> essentialPresets = [];
    private readonly List<SettingsMultiSelect> essentialMultis = [];
#if GRATIS_PREVIEW
    public readonly Vector4[] TestLookChips = new Vector4[3];
#endif

    // Resolved on first use: the catalog is built by another static initializer of this type.
    private static (EssentialCard Card, SettingsEntry[] Entries, SettingsPreset[] Presets,
        SettingsMultiSelect[] MultiSelects)[] ResolveEssentials() =>
        essentialCards ??= Array.ConvertAll(EssentialDefinitions, card => (card, Array.ConvertAll(card.Paths,
            path => Array.Find(Entries, entry => entry.Path == path)
                ?? throw new InvalidOperationException("Essentials setting not in the catalog: " + path)),
            Array.ConvertAll(card.PresetIds ?? [], id => Array.Find(Presets, preset => preset.Id == id)
                ?? throw new InvalidOperationException("Essentials preset not in the catalog: " + id)),
            Array.ConvertAll(card.MultiIds ?? [], id => Array.Find(MultiSelects, multi => multi.Id == id)
                ?? throw new InvalidOperationException("Essentials multi-select not in the catalog: " + id))));

    internal static (string Title, SettingsEntry[] Entries)[] EssentialEntries() =>
        Array.ConvertAll(ResolveEssentials(), card => (card.Card.Title, card.Entries));

    internal static (string Title, SettingsPreset[] Presets)[] EssentialPresets() =>
        Array.ConvertAll(ResolveEssentials(), card => (card.Card.Title, card.Presets));

    internal static (string Title, SettingsMultiSelect[] MultiSelects)[] EssentialMultiSelects() =>
        Array.ConvertAll(ResolveEssentials(), card => (card.Card.Title, card.MultiSelects));

    private bool DrawEssentials(SystemConfig config, float scale) {
        var cards = ResolveEssentials();
        var changed = false;
        ImGui.PushID("essentials");
        DrawPageTitle(Tabs[EssentialsTab], TabHelpers[EssentialsTab]);
        foreach (var (card, entries, presets, multiSelects) in cards) {
            essentialRows.Clear();
            essentialPresets.Clear();
            essentialMultis.Clear();
            foreach (var entry in entries)
                if (entry.Visible(config) && entry.Enabled(config))
                    essentialRows.Add(entry);
            essentialPresets.AddRange(presets);
            essentialMultis.AddRange(multiSelects);
            if (essentialRows.Count == 0 && essentialPresets.Count == 0 && essentialMultis.Count == 0)
                continue;
            if (!SectionHeader.Settings(card.HeaderId, card.Title, card.Icon, true, scale))
                continue;
            var cardStart = ImGui.GetCursorScreenPos();
            changed |= DrawEntryRows(config, essentialRows, card.RowsId, essentials: true, presets: essentialPresets,
                multiSelects: essentialMultis);
            var bottom = ImGui.GetCursorScreenPos().Y - ImGui.GetStyle().ItemSpacing.Y;
            ImGui.GetWindowDrawList().AddRect(cardStart,
                new Vector2(cardStart.X + ImGui.GetContentRegionAvail().X, bottom),
                ImGui.GetColorU32(GratisTheme.Border), GratisTheme.Radius.Card * scale);
            ImGui.Spacing();
        }
        ImGui.Spacing();
        if (ChromeControls.TextButton("Run Quick setup again", scale: scale))
            SettingsQuickSetupDrawing.Run(config);
        ImGui.SameLine();
        if (ChromeControls.TextButton("Manage profiles", scale: scale))
            SelectTab(AppearanceTab, Sub.Look);
        ImGui.Spacing();
        ImGui.PopID();
        return changed;
    }

    /// <summary>The three named looks as chips, applied the way Quick setup's Look step applies them.</summary>
    private bool DrawLookChips(SystemConfig config, float scale) {
        var looks = QuickSetupLooks.All;
        var gap = GratisTheme.Grid * scale;
        var width = MathF.Max(1, (ImGui.GetContentRegionAvail().X - gap * (looks.Count - 1)) / looks.Count);
        var height = (GratisTheme.ControlHeight - 4) * scale;
        var current = QuickSetupLooks.Current(config);
        var changed = false;
        for (var i = 0; i < looks.Count; i++) {
            if (i > 0)
                ImGui.SameLine(0, gap);
            ImGui.PushID(i);
            var clicked = SegmentedChip.Draw("##look", looks[i].Name, i == current,
                new(ChipFaces.Regular, GratisTheme.WarmAccent, new Vector2(width, height), scale, Centered: true));
            ChromeControls.Tooltip(looks[i].Blurb, scale: scale);
#if GRATIS_PREVIEW
            TestLookChips[i] = new(ImGui.GetItemRectMin().X, ImGui.GetItemRectMin().Y, ImGui.GetItemRectMax().X,
                ImGui.GetItemRectMax().Y);
#endif
            ImGui.PopID();
            if (clicked && i != current)
                changed |= QuickSetupLooks.Apply(config, i) > 0;
        }
        return changed;
    }

    /// <summary>One line under a label, cut to the label column; the full text is the row's tooltip.</summary>
    private void DrawHelperLine(string key, string text, float width) {
        if (!helperLabels.TryGetValue(key, out var cache))
            helperLabels.Add(key, cache = new());
        using var font = SettingsTheme.Font(12);
        ImGui.TextColored(GratisTheme.Secondary, cache.Fit(text, MathF.Max(1, width)));
    }
}
