using System;
using System.Linq;
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
    public static readonly string[] Sections = [
        "Appearance",
        "Map",
        "Markers",
        "Chrome & motion",
        "Modules / tools",
        "Advanced",
        "Filters",
        "Tweaks"];
    private static readonly char[] SectionIcons = [GratisIcons.Eye, GratisIcons.Compass, GratisIcons.MapPin,
        GratisIcons.Layers, GratisIcons.Flag, GratisIcons.Settings, GratisIcons.LayoutGrid, GratisIcons.Wrench];
    public string Search = "";
    public int SelectedSection;
    public static SettingsEntry[] Entries { get; } = CreateEntries();

    public static SettingsEntry[] Find(SystemConfig config, string section, string search) {
        _ = config.FilterOptions;
        return Entries.Where(entry => entry.Section == section && entry.Visible(config)
            && entry.Matches(search)).ToArray();
    }

    public static void Reset(SystemConfig config, string section) {
        if (section == "Tweaks") {
            foreach (var tweak in TweakCatalog.All)
                tweak.Reset(config);
            return;
        }
        var entries = Find(config, section, "");
        var defaults = new SystemConfig();
        foreach (var entry in entries)
            entry.Reset(config, defaults);
        if (section == "Filters" && config.IsGratis) {
            config.ResetFilterBar();
            MapDrawContext.Current?.Visibility.ResetFilters();
        }
        if (section == "Markers" && config.IsGratis) {
            config.ResetMarkers();
            config.MarkerStyle = 1;
            config.IconScale = defaults.IconScale;
        }
    }

    public bool Draw(SystemConfig config, Func<string, string, bool>? drawExtension = null,
        Action<string>? resetExtension = null) {
        if (config.IsGratis)
            return DrawGratis(config, drawExtension, resetExtension);
        return DrawClassic(config, drawExtension, resetExtension);
    }

    private bool DrawClassic(SystemConfig config, Func<string, string, bool>? drawExtension,
        Action<string>? resetExtension) {
        config.MapColour ??= new MapColourSettings();
        config.Motion ??= new MotionSettings();
        config.Markers ??= new MarkerSettings();
        var changed = false;
        var scale = ImGui.GetFontSize() / 18;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10, 9) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 5) * scale);
        try {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##settings-search", "Search settings", ref Search, 128);
            ImGui.Spacing();
            var available = ImGui.GetContentRegionAvail();
            var navWidth = 180 * scale;
            if (ImGui.BeginChild("settings-navigation", new Vector2(navWidth, available.Y))) {
                for (var index = 0; index < Sections.Length; index++) {
                    if (Sections[index] == "Filters")
                        continue;
                    var start = ImGui.GetCursorScreenPos();
                    var rowHeight = 38 * scale;
                    if (ImGui.Selectable($"##section-{index}", SelectedSection == index, ImGuiSelectableFlags.None,
                        new Vector2(navWidth - 8 * scale, rowHeight))) {
                        SelectedSection = index;
                        Search = "";
                    }
                    GratisIcons.Draw(ImGui.GetWindowDrawList(), start + new Vector2(13 * scale, rowHeight / 2),
                        SectionIcons[index], SelectedSection == index ? GratisTheme.Accent : GratisTheme.Secondary,
                        16 * scale);
                    ImGui.GetWindowDrawList().AddText(start + new Vector2(29, 10) * scale,
                        ImGui.GetColorU32(SelectedSection == index ? GratisTheme.Primary : GratisTheme.Secondary),
                        Sections[index]);
                }
            }
            ImGui.EndChild();
            ImGui.SameLine();
            if (ImGui.BeginChild("settings-content", new Vector2(0, available.Y))) {
                var tweaksPage = Sections[SelectedSection] == "Tweaks";
                if (tweaksPage)
                    changed |= DrawTweaks(config);
                var sectionNames = tweaksPage ? Array.Empty<string>() : string.IsNullOrWhiteSpace(Search)
                    ? new[] { Sections[SelectedSection] } : Sections;
                var found = false;
                foreach (var section in sectionNames) {
                    var entries = Find(config, section, Search);
                    var extension = ExtensionMatches(config, section, Search);
                    if (entries.Length == 0 && !extension)
                        continue;
                    found = true;
                    ImGui.PushID(section);
                    var start = ImGui.GetCursorScreenPos();
                    var icon = Array.IndexOf(Sections, section);
                    GratisIcons.Draw(ImGui.GetWindowDrawList(), start + new Vector2(10, 12) * scale, SectionIcons[icon],
                        GratisTheme.Accent, 18 * scale);
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 26 * scale);
                    ImGui.TextUnformatted(SectionLabel(section));
                    ImGui.SameLine(ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X
                        - ImGui.CalcTextSize("Reset section").X - ImGui.GetStyle().FramePadding.X * 2);
                    if (ImGui.SmallButton("Reset section")) {
                        Reset(config, section);
                        resetExtension?.Invoke(section);
                        changed = true;
                    }
                    if (ImGui.IsItemHovered())
                        UiMotion.TooltipText("Restore this section for the current style. Per-icon overrides have individual resets.");
                    ImGui.Separator();
                    if (section == "Markers" && config.IsGratis && MarkerOptionsMatch(Search)
                        && drawExtension is not null)
                        changed |= drawExtension("Marker options", Search);
                    foreach (var group in entries.GroupBy(entry => entry.Group)) {
                        var rows = group.Where(entry => entry.Enabled(config)
                            || !string.IsNullOrWhiteSpace(Search)).ToArray();
                        if (rows.Length == 0)
                            continue;
                        if (!string.IsNullOrWhiteSpace(Search))
                            ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                        if (!ImGui.CollapsingHeader(GroupLabel(group.Key), ImGuiTreeNodeFlags.DefaultOpen))
                            continue;
                        if (ImGui.BeginTable($"rows-{group.Key}", 2, ImGuiTableFlags.SizingStretchProp)) {
                            ImGui.TableSetupColumn("Setting", ImGuiTableColumnFlags.WidthStretch, 1.15f);
                            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch, 1);
                            foreach (var entry in rows) {
                                ImGui.PushID(entry.Path);
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.AlignTextToFramePadding();
                                ImGui.TextUnformatted(entry.Label);
                                if (entry.Tooltip is { } tip && ImGui.IsItemHovered())
                                    UiMotion.TooltipText(tip);
                                ImGui.TableNextColumn();
                                ImGui.SetNextItemWidth(-1);
                                var enabled = entry.Enabled(config);
                                ImGui.BeginDisabled(!enabled);
                                changed |= entry.Draw(config);
                                ImGui.EndDisabled();
                                if (entry.Tooltip is { } valueTip && ImGui.IsItemHovered())
                                    UiMotion.TooltipText(valueTip);
                                ImGui.PopID();
                            }
                            ImGui.EndTable();
                        }
                        ImGui.Spacing();
                    }
                    if (section == "Map" && string.IsNullOrWhiteSpace(Search)
                        && ImGui.Button("Marker declutter settings"))
                        SelectedSection = 2;
                    if (extension && drawExtension is not null)
                        changed |= drawExtension(section, Search);
                    ImGui.Spacing();
                    ImGui.PopID();
                }
                if (!found && !tweaksPage)
                    ImGui.TextDisabled("No settings match. Try another name or change the visual style.");
            }
            ImGui.EndChild();
        }
        finally { ImGui.PopStyleVar(2); }
        return changed;
    }

    public static bool ExtensionMatches(SystemConfig config, string section, string search) {
        return section switch {
            "Markers" => config.IsGratis && MarkerOptionsMatch(search),
            "Modules / tools" => NamesMatch(search, "FATE list quest list flag history limit"),
            _ => false,
        };
    }

    private static bool NamesMatch(string search, string names) => string.IsNullOrWhiteSpace(search)
        || search.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word =>
            names.Contains(word, StringComparison.OrdinalIgnoreCase));

    public static bool MarkerOptionsMatch(string search) =>
        NamesMatch(search, "markers icons shapes game declutter zoom minimum maximum scale hide low "
            + "priority cluster distance dim irrelevant opacity priority");
    public static bool DrawTools(string search, ref int historyLimit, Action<string>? activate) {
        if (MapDrawContext.Current.Config.IsGratis) {
            var s = SettingsTheme.Scale;
            foreach (var (tool, label, glyph) in new[] { ("FATE list", "FATEs", GratisIcons.Swords),
                ("Quest list", "Quests", GratisIcons.BookOpen), ("Flag history", "Flag history", GratisIcons.Flag) }) {
                if (!NamesMatch(search, tool))
                    continue;
                ImGui.BeginDisabled(activate is null);
                var pos = ImGui.GetCursorScreenPos();
                var size = new Vector2(MathF.Min(280 * s, ImGui.GetContentRegionAvail().X),
                    GratisTheme.ControlHeight * s);
                if (ImGui.Button("##open-" + tool, size))
                    activate?.Invoke(tool);
                GratisIcons.Draw(ImGui.GetWindowDrawList(), pos + new Vector2(18 * s, size.Y / 2), glyph,
                    GratisTheme.Secondary, GratisTheme.IconSize * s);
                ImGui.GetWindowDrawList().AddText(pos + new Vector2(36 * s, (size.Y - ImGui.GetFontSize()) / 2),
                    ImGui.GetColorU32(GratisTheme.Primary), label);
                ImGui.EndDisabled();
            }
            if (!NamesMatch(search, "Flag history limit"))
                return false;
            ImGui.TextUnformatted("Recent flags to keep");
            ImGui.SetNextItemWidth(MathF.Min(280 * s, ImGui.GetContentRegionAvail().X));
            float count = historyLimit;
            var changed = SettingsTheme.Slider("##flag-history-limit", ref count, 1, 100, "%d flags",
                GratisTheme.WarmAccent, true);
            if (changed)
                historyLimit = (int)count;
            return changed;
        }
        foreach (var tool in new[] { "FATE list", "Quest list", "Flag history" }) {
            if (!NamesMatch(search, tool))
                continue;
            ImGui.BeginDisabled(activate is null);
            if (ImGui.Button($"Open {tool}", new Vector2(210, 0)))
                activate?.Invoke(tool);
            ImGui.EndDisabled();
        }
        if (!NamesMatch(search, "Flag history limit"))
            return false;
        ImGui.SetNextItemWidth(210);
        return ImGui.SliderInt("Flag history limit", ref historyLimit, 1, 100, "%d flags");
    }
}