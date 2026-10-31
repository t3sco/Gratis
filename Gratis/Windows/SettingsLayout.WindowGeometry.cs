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

/// <summary>
/// The map window's position and size have no rows: drag and resize, window presets and this reset cover them. The
/// reset is the way back for a window lost off-screen, so it is always shown, never behind Show advanced.
/// </summary>
public sealed partial class SettingsLayout {
    private const string GeometryLabel = "Window size and position", ResetGeometryLabel = "Reset window position and size";
    private const string ResetGeometryTip = "Put the map window back at its default size and position on your main screen.";
#if GRATIS_PREVIEW
    public Vector4 TestResetWindow;
#endif

    internal static bool GeometryMatches(string search) =>
        NamesMatch(search, "window size position geometry reset recover lost off-screen offscreen");

    /// <summary>The Gratis card, laid out like a catalog group: header, then one full-width button.</summary>
    private bool DrawWindowGeometry(SystemConfig config, bool searching, float scale) {
        if (searching)
            ImGui.SetNextItemOpen(true, ImGuiCond.Always);
        var open = SectionHeader.Settings("##group-Window geometry", GeometryLabel, GratisIcons.LayoutGrid, true, scale);
#if GRATIS_PREVIEW
        TestGroups.Add("Window geometry");
#endif
        if (!open)
            return false;
        var cardStart = ImGui.GetCursorScreenPos();
        var changed = false;
        if (ImGui.BeginTable("rows-Window geometry", 1, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX)) {
            ImGui.TableNextRow(0, GratisTheme.ControlHeight * scale);
            ImGui.TableNextColumn();
            changed = ResetWindowButton(config, scale);
            ImGui.EndTable();
        }
        var bottom = ImGui.GetCursorScreenPos().Y - ImGui.GetStyle().ItemSpacing.Y;
        ImGui.GetWindowDrawList().AddRect(cardStart,
            new Vector2(cardStart.X + ImGui.GetContentRegionAvail().X, bottom),
            ImGui.GetColorU32(GratisTheme.Border), GratisTheme.Radius.Card * scale);
        ImGui.Spacing();
        return changed;
    }

    private bool ResetWindowButton(SystemConfig config, float scale) {
        var clicked = ChromeControls.TextButton(ResetGeometryLabel, new Vector2(ImGui.GetContentRegionAvail().X, 0),
            scale: scale);
#if GRATIS_PREVIEW
        TestResetWindow = new(ImGui.GetItemRectMin().X, ImGui.GetItemRectMin().Y, ImGui.GetItemRectMax().X,
            ImGui.GetItemRectMax().Y);
#endif
        ChromeControls.Tooltip(ResetGeometryTip, scale: scale);
        if (clicked)
            MapDrawContext.Current.WindowPresets.ResetCurrent(config);
        return clicked;
    }

    private bool DrawWindowGeometryClassic(SystemConfig config) {
        if (!ImGui.CollapsingHeader(GeometryLabel, ImGuiTreeNodeFlags.DefaultOpen))
            return false;
        var clicked = ImGui.Button(ResetGeometryLabel);
#if GRATIS_PREVIEW
        TestResetWindow = new(ImGui.GetItemRectMin().X, ImGui.GetItemRectMin().Y, ImGui.GetItemRectMax().X,
            ImGui.GetItemRectMax().Y);
#endif
        if (ImGui.IsItemHovered())
            UiMotion.TooltipText(ResetGeometryTip);
        if (clicked)
            MapDrawContext.Current.WindowPresets.ResetCurrent(config);
        ImGui.Spacing();
        return clicked;
    }
}
