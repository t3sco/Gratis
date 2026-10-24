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

public sealed partial class SettingsLayout
{
    private bool DrawGratis(SystemConfig config, Func<string, string, bool>? drawExtension, Action<string>? resetExtension)
    {
        config.MapColour ??= new MapColourSettings();
        config.Motion ??= new MotionSettings();
        config.Markers ??= new MarkerSettings();
        var scale = SettingsTheme.Scale;
        var changed = false;
        var list = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(32, 8) * scale);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##settings-search", "Search settings...", ref Search, 128);
        ImGui.PopStyleVar();
        GratisIcons.Draw(list, start + new Vector2(16, 15) * scale, GratisIcons.Search, GratisTheme.Secondary, 14 * scale);
        if (Search.Length > 0) {
            var next = ImGui.GetCursorPos();
            ImGui.SetCursorScreenPos(start + new Vector2(width - 30 * scale, 3 * scale));
            if (ImGui.Button("##clear-search", new Vector2(24) * scale)) Search = "";
            GratisIcons.Draw(list, start + new Vector2(width - 18 * scale, 15 * scale), GratisIcons.X, GratisTheme.Secondary, 12 * scale);
            ImGui.SetCursorPos(next);
        }
        ImGui.Spacing();
        var available = ImGui.GetContentRegionAvail();
        var navWidth = 184 * scale;
        var navStart = ImGui.GetCursorScreenPos();
        if (ImGui.BeginChild("settings-navigation", new Vector2(navWidth, available.Y))) {
            using (SettingsTheme.Font(12)) {
                ImGui.TextColored(GratisTheme.Secondary, "SETTINGS");
            }
            ImGui.Spacing();
            for (var index = 0; index < Sections.Length; index++) {
                start = ImGui.GetCursorScreenPos();
                var size = new Vector2(navWidth - 12 * scale, 36 * scale);
                if (ImGui.Selectable($"##section-{index}", SelectedSection == index, ImGuiSelectableFlags.None, size)) {
                    SelectedSection = index; Search = "";
                }
                var selected = SelectedSection == index;
                var nav = ImGui.GetWindowDrawList();
                if (selected) {
                    nav.AddRectFilled(start, start + size, ImGui.GetColorU32(new Vector4(1, 1, 1, .04f)), 4 * scale);
                    nav.AddRectFilled(start + new Vector2(0, 8) * scale, start + new Vector2(2, 28) * scale,
                        ImGui.GetColorU32(config.AccentColor), scale);
                }
                GratisIcons.Draw(nav, start + new Vector2(18, 18) * scale, SectionIcons[index],
                    selected ? config.AccentColor : GratisTheme.Secondary, 16 * scale);
                nav.AddText(start + new Vector2(34 * scale, (size.Y - ImGui.GetFontSize()) / 2),
                    ImGui.GetColorU32(selected ? GratisTheme.Primary : GratisTheme.Secondary), Sections[index]);
            }
        }
        ImGui.EndChild();
        list.AddLine(navStart + new Vector2(navWidth, 0), navStart + new Vector2(navWidth, available.Y), ImGui.GetColorU32(GratisTheme.Border));
        ImGui.SameLine(0, 12 * scale);
        if (ImGui.BeginChild("settings-content", new Vector2(0, available.Y))) {
            var sections = string.IsNullOrWhiteSpace(Search) ? new[] { Sections[SelectedSection] } : Sections;
            var found = false;
            foreach (var section in sections) {
                var entries = Find(config, section, Search);
                var extension = ExtensionMatches(config, section, Search);
                if (entries.Length == 0 && !extension) continue;
                found = true;
                ImGui.PushID(section);
                using (SettingsTheme.Font(14, ChromeFontRole.Title)) {
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(section);
                }
                ImGui.SameLine(ImGui.GetWindowWidth() - ImGui.CalcTextSize("Reset section").X - 24 * scale);
                ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
                ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Secondary);
                if (ImGui.Button("Reset section")) { Reset(config, section); resetExtension?.Invoke(section); changed = true; }
                ImGui.PopStyleColor(); ImGui.PopStyleVar();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Restore this section for the current style. Per-icon overrides have individual resets.");
                SettingsTheme.Helper(section switch {
                    "Appearance" => "Make the map feel at home.",
                    "Map" => "Viewport, navigation and map detail.",
                    "Markers" => "Choose what stands out on your map.",
                    "Chrome & motion" => "Quiet controls. Deliberate movement.",
                    "Modules / tools" => "Your map's companion tools.",
                    _ => "Window behaviour and diagnostics.",
                });
                ImGui.Spacing();
                if (section == "Markers" && MarkerOptionsMatch(Search) && drawExtension is not null)
                    changed |= drawExtension("Marker options", Search);
                foreach (var group in entries.GroupBy(entry => entry.Group)) {
                    var rows = group.Where(entry => entry.Enabled(config) || !string.IsNullOrWhiteSpace(Search)).ToArray();
                    if (rows.Length == 0) continue;
                    if (!string.IsNullOrWhiteSpace(Search)) ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                    bool open;
                    using (SettingsTheme.Font(12)) {
                        ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Secondary);
                        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
                        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 4) * scale);
                        open = ImGui.CollapsingHeader(group.Key.ToUpperInvariant(), ImGuiTreeNodeFlags.DefaultOpen);
                        ImGui.PopStyleVar(2);
                        ImGui.PopStyleColor();
                    }
                    ImGui.Separator();
                    if (!open) continue;
                    var cardStart = ImGui.GetCursorScreenPos();
                    if (ImGui.BeginTable($"rows-{group.Key}", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX)) {
                        ImGui.TableSetupColumn("Setting", ImGuiTableColumnFlags.WidthStretch, 1.1f);
                        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch, 1);
                        foreach (var entry in rows) {
                            ImGui.PushID(entry.Path);
                            ImGui.TableNextRow(0, 36 * scale);
                            ImGui.TableNextColumn();
                            ImGui.AlignTextToFramePadding();
                            ImGui.TextUnformatted(entry.Label);
                            if (entry.Tooltip is { } tip && ImGui.IsItemHovered()) ImGui.SetTooltip(tip);
                            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                            ImGui.BeginDisabled(!entry.Enabled(config));
                            changed |= entry.Draw(config);
                            ImGui.EndDisabled();
                            if (entry.Tooltip is { } valueTip && ImGui.IsItemHovered()) ImGui.SetTooltip(valueTip);
                            ImGui.PopID();
                        }
                        ImGui.EndTable();
                    }
                    var bottom = ImGui.GetCursorScreenPos().Y - ImGui.GetStyle().ItemSpacing.Y;
                    ImGui.GetWindowDrawList().AddRect(cardStart, new Vector2(cardStart.X + ImGui.GetContentRegionAvail().X, bottom),
                        ImGui.GetColorU32(GratisTheme.Border), GratisTheme.CardRadius * scale);
                    ImGui.Spacing();
                }
                if (section == "Map" && string.IsNullOrWhiteSpace(Search) && ImGui.Button("Marker declutter settings")) SelectedSection = 2;
                if (extension && drawExtension is not null) changed |= drawExtension(section, Search);
                ImGui.Spacing(); ImGui.PopID();
            }
            if (!found) SettingsTheme.Helper("No settings match. Try another name or change the visual style.");
        }
        ImGui.EndChild();
        return changed;
    }
}
