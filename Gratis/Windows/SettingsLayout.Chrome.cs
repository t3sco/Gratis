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
    internal static string SectionLabel(string section) => section switch {
        "Chrome & motion" => "Controls & motion",
        "Modules / tools" => "Tools",
        _ => section,
    };
    internal static string GroupLabel(string group) => group switch {
        "Chrome visibility" => "Auto-hide controls",
        "Viewport" => "View",
        "Glass surfaces" => "Frosted panels",
        "Conic ring" => "Gradient border",
        "Edge feather" => "Soft edge",
        "Legacy zoom scale" => "Icon zoom",
        "Map backdrop" => "Background blur",
        "Native window" => "Game window frame",
        "Chrome layout" => "Control positions",
        "Gratis toolbar" => "Toolbar",
        "Gratis title" => "Location card",
        "Gratis coordinates" => "Coordinates",
        "Categories in the list" => "Categories in your drawer",
        "Gratis style" => "Map style",
        _ => group,
    };
    private static bool ExpandedByDefault(string group) => group is not
        ("Categories in the list" or "Conic ring" or "Edge feather");
    private static char GroupIcon(string group) => group switch {
        "Travel" or "Place search" => GratisIcons.Compass,
        "Chrome layout" => GratisIcons.Anchor,
        "Chrome visibility" or "Visibility and interaction" or "Categories in the list" => GratisIcons.Eye,
        "Arrival notices" => GratisIcons.Flag,
        "Lenses" or "Gratis toolbar" => GratisIcons.Layers,
        "Coordinates" or "Gratis coordinates" or "Viewport" or "Zoom" => GratisIcons.LocateFixed,
        "Motion" or "Player" or "Gratis player" => GratisIcons.Footprints,
        "Pins and notes" or "Marker colours and size" => GratisIcons.MapPin,
        "Map recolour" or "Glass surfaces" or "Edge feather" or "Conic ring" or "Frame effects" => GratisIcons.Sparkles,
        _ => GratisIcons.Settings,
    };
    /// <summary>The label the Gratis settings page shows; search matches this, not the catalog label behind it.</summary>
    internal static string RowLabel(SettingsEntry entry) => entry.Path switch {
        "AetheryteChips" => "Nearest aetheryte card",
        "TravelActions" => "Teleport actions",
        "TravelConfirmGil" => "Confirm travel from",
        "ShowEorzeaTime" => "Show Eorzea time",
        _ => entry.Label,
    };
#if GRATIS_PREVIEW
    public readonly global::System.Collections.Generic.List<(string Path, Vector4 Label,
        Vector4 Control)> TestRows = [];
    public Vector4 TestNavigation, TestContent;
#endif
    /// <summary>
    /// Draws catalog rows as label and control. Essentials rows also show the catalog tooltip as a one-line helper
    /// under the label, and its Look row is the three-look chip selector instead of the visual style combo.
    /// </summary>
    private bool DrawEntryRows(SystemConfig config, List<SettingsEntry> entries, string id, bool essentials = false,
        List<SettingsPreset>? presets = null, List<SettingsMultiSelect>? multiSelects = null) {
        if (entries.Count == 0 && presets is not { Count: > 0 } && multiSelects is not { Count: > 0 })
            return false;
        var s = SettingsTheme.Scale;
        var width = ImGui.GetContentRegionAvail().X;
        var stacked = width < 360 * s;
        var changed = false;
        if (!ImGui.BeginTable(id, stacked ? 1 : 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX))
            return false;
        ImGui.TableSetupColumn("Setting",
            stacked ? ImGuiTableColumnFlags.WidthStretch : ImGuiTableColumnFlags.WidthFixed,
            essentials ? MathF.Min(300 * s, width * .56f) : MathF.Min(220 * s, width * .48f));
        if (!stacked)
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
        if (presets is not null)
            foreach (var preset in presets)
                changed |= DrawPresetRow(config, preset, s, stacked, essentials);
        if (multiSelects is not null)
            foreach (var multi in multiSelects)
                changed |= DrawMultiSelectRow(config, multi, s, stacked, essentials);
        foreach (var entry in entries) {
            ImGui.PushID(entry.Path);
            ImGui.TableNextRow(0, GratisTheme.ControlHeight * s);
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            var cellStart = ImGui.GetCursorScreenPos();
            var cellWidth = ImGui.GetContentRegionAvail().X;
            var inlineToggle = stacked && entry.Value(config) is bool;
            if (entry.Path.StartsWith("Filters.", StringComparison.Ordinal)
                && Enum.TryParse<MarkerGroup>(entry.Path[8..], out var category)) {
                var pos = ImGui.GetCursorScreenPos();
                ImGui.Dummy(new Vector2(16 * s, ImGui.GetFontSize()));
                ImGui.SameLine(0, 8 * s);
                GratisIcons.Draw(ImGui.GetWindowDrawList(), pos + new Vector2(8 * s, ImGui.GetFontSize() / 2),
                    category == MarkerGroup.Other ? GratisIcons.Info : MarkerVisibility.Icons[(int)category],
                    GratisTheme.FilterTints[(int)category], 14 * s);
            }
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X
                - (inlineToggle ? 52 * s : 0));
            ImGui.TextUnformatted(essentials && entry.Path == "VisualStyle" ? "Look" : RowLabel(entry));
            ImGui.PopTextWrapPos();
#if GRATIS_PREVIEW
            var labelMin = ImGui.GetItemRectMin(); var labelMax = ImGui.GetItemRectMax();
#endif
            if (entry.Tooltip is { } tip) {
                ChromeControls.Tooltip(tip, scale: s);
                if (essentials)
                    DrawHelperLine(entry.Path, tip, cellWidth - (inlineToggle ? 52 * s : 0));
            }
            if (!stacked)
                ImGui.TableNextColumn();
            else if (inlineToggle) {
                ImGui.SameLine();
                ImGui.SetCursorScreenPos(new(cellStart.X + cellWidth - 36 * s, cellStart.Y));
            }
            ImGui.SetNextItemWidth(-1);
            using (var disabled = UiMotion.Disabled("##setting-enabled", entry.Enabled(config))) {
                ImGui.BeginGroup();
                changed |= essentials && entry.Path == "VisualStyle" ? DrawLookChips(config, s) : entry.Draw(config);
                ImGui.EndGroup();
            }
            if (entry.Tooltip is { } valueTip)
                ChromeControls.Tooltip(valueTip, scale: s);
#if GRATIS_PREVIEW
            var controlMin = ImGui.GetItemRectMin(); var controlMax = ImGui.GetItemRectMax();
            TestRows.Add((entry.Path, new(labelMin.X, labelMin.Y, labelMax.X, labelMax.Y),
                new(controlMin.X, controlMin.Y, controlMax.X, controlMax.Y)));
#endif
            ImGui.PopID();
        }
        ImGui.EndTable();
        return changed;
    }
}