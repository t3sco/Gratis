using System;
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
    private static readonly string[] NavigationIds = [
        "##section-0",
        "##section-1",
        "##section-2",
        "##section-3",
        "##section-4",
        "##section-5",
        "##section-6",
        "##section-7"];
    private static readonly int[] NavigationOrder = [7, 0, 1, 2, 6, 3, 4, 5];
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
        "Window geometry" => "Window size and position",
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
        ("Categories in the list" or "Glass surfaces" or "Conic ring" or "Edge feather");
    private static char GroupIcon(string group) => group switch {
        "Travel" or "Place search" => GratisIcons.Compass,
        "Chrome layout" => GratisIcons.Anchor,
        "Chrome visibility" or "Visibility and interaction" or "Filter bar" or "Categories in the list" =>
            GratisIcons.Eye,
        "Lenses" or "Gratis toolbar" => GratisIcons.Layers,
        "Coordinates" or "Gratis coordinates" or "Viewport" or "Zoom" => GratisIcons.LocateFixed,
        "Motion" or "Player" or "Gratis player" => GratisIcons.Footprints,
        "Pins and notes" or "Marker colours and size" => GratisIcons.MapPin,
        "Map recolour" or "Glass surfaces" or "Edge feather" or "Conic ring" => GratisIcons.Sparkles,
        _ => GratisIcons.Settings,
    };
    private static bool FineTune(SettingsEntry entry) => entry.Path.StartsWith("Motion.", StringComparison.Ordinal)
        || entry.Path is
        "ChromeIdleDelay" or "ChromeFadeDurationMs" or "FlagArrivalRadius" or "SpotlightDim" or "SpotlightPulse" or "SpotlightSeconds" or "AutoZoomFactor";
    internal static string RowLabel(SettingsEntry entry) => entry.Path switch {
        "CompactTitle" => "Compact location card",
        "StyledCoordinates" => "Coordinate card",
        "AetheryteChips" => "Nearest aetheryte card",
        "TravelActions" => "Teleport actions",
        "TravelConfirmGil" => "Confirm travel from",
        "Filters.Mode" => "Drawer mode",
        "ChromeFont" => "Control font",
        "QuickFilterBar" => "Show filter bar",
        "ShowEorzeaTime" => "Show Eorzea time",
        _ => entry.Label,
    };
#if GRATIS_PREVIEW
    public readonly global::System.Collections.Generic.List<(string Path, Vector4 Label,
        Vector4 Control)> TestRows = [];
    public Vector4 TestNavigation, TestContent;
#endif
    private bool DrawEntryRows(SystemConfig config, SettingsEntry[] entries, string id) {
        if (entries.Length == 0)
            return false;
        var s = SettingsTheme.Scale;
        var width = ImGui.GetContentRegionAvail().X;
        var stacked = width < 360 * s;
        var changed = false;
        if (!ImGui.BeginTable(id, stacked ? 1 : 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX))
            return false;
        ImGui.TableSetupColumn("Setting",
            stacked ? ImGuiTableColumnFlags.WidthStretch : ImGuiTableColumnFlags.WidthFixed,
            MathF.Min(220 * s, width * .48f));
        if (!stacked)
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
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
            ImGui.TextUnformatted(RowLabel(entry));
            ImGui.PopTextWrapPos();
#if GRATIS_PREVIEW
            var labelMin = ImGui.GetItemRectMin(); var labelMax = ImGui.GetItemRectMax();
#endif
            if (entry.Tooltip is { } tip)
                ChromeControls.Tooltip(tip, scale: s);
            if (!stacked)
                ImGui.TableNextColumn();
            else if (inlineToggle) {
                ImGui.SameLine();
                ImGui.SetCursorScreenPos(new(cellStart.X + cellWidth - 36 * s, cellStart.Y));
            }
            ImGui.SetNextItemWidth(-1);
            using (var disabled = UiMotion.Disabled("##setting-enabled", entry.Enabled(config))) {
                ImGui.BeginGroup();
                changed |= entry.Draw(config);
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