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
    private readonly Dictionary<string, ActionLabelCache> multiPreviews = [];
#if GRATIS_PREVIEW
    public readonly List<(string Id, Vector4 Label, Vector4 Control, string Summary)> TestMultiRows = [];
    public readonly List<(string Id, int Item, Vector4 Bounds)> TestMultiItems = [];
#endif

    /// <summary>A multi-select as a row of the settings table: its label, then a dropdown of checkboxes.</summary>
    private bool DrawMultiSelectRow(SystemConfig config, SettingsMultiSelect multi, float s, bool stacked, bool essentials) {
        ImGui.PushID(multi.Id);
        ImGui.TableNextRow(0, GratisTheme.ControlHeight * s);
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        var cellWidth = ImGui.GetContentRegionAvail().X;
        ImGui.TextUnformatted(multi.Label);
#if GRATIS_PREVIEW
        var labelMin = ImGui.GetItemRectMin(); var labelMax = ImGui.GetItemRectMax();
#endif
        ChromeControls.Tooltip(multi.Tooltip, scale: s);
        if (essentials)
            DrawHelperLine("multi-" + multi.Id, multi.Tooltip, cellWidth);
        if (!stacked)
            ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        ImGui.BeginGroup();
        var changed = DrawMultiSelectCombo(config, multi);
        ImGui.EndGroup();
#if GRATIS_PREVIEW
        var controlMin = ImGui.GetItemRectMin(); var controlMax = ImGui.GetItemRectMax();
        TestMultiRows.Add((multi.Id, new(labelMin.X, labelMin.Y, labelMax.X, labelMax.Y),
            new(controlMin.X, controlMin.Y, controlMax.X, controlMax.Y), multi.Summary(config)));
#endif
        ImGui.PopID();
        return changed;
    }

    /// <summary>The same checkbox-in-combo widget the Fade map row uses, with a cached summary as its preview text.</summary>
    private bool DrawMultiSelectCombo(SystemConfig config, SettingsMultiSelect multi) {
        if (!multiPreviews.TryGetValue(multi.Id, out var preview))
            multiPreviews.Add(multi.Id, preview = new());
        // Same height and arrow as the chrome combos beside it; the popup keeps the stock checkbox-in-combo look.
        var s = SettingsTheme.Scale;
        var room = ImGui.CalcItemWidth() - 38 * s;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,
            new Vector2(10 * s, MathF.Max(0, (GratisTheme.ControlHeight * s - ImGui.GetFontSize()) / 2)));
        var open = ImGui.BeginCombo("##multi", preview.Fit(multi.Summary(config), MathF.Max(1, room)),
            ImGuiComboFlags.NoArrowButton);
        ImGui.PopStyleVar();
        var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax();
        var center = new Vector2(max.X - 15 * s, (min.Y + max.Y) / 2);
        ImGui.GetWindowDrawList().AddTriangleFilled(center + new Vector2(-4, -2) * s, center + new Vector2(4, -2) * s,
            center + new Vector2(0, 3) * s, ImGui.GetColorU32(GratisTheme.Secondary));
        var changed = false;
        if (open) {
            for (var i = 0; i < multi.ItemCount; i++) {
                var on = multi.IsOn(config, i);
                ImGui.PushID(i);
                if (SettingsTheme.Toggle("##item", ref on, multi.ItemLabels[i])) {
                    multi.Set(config, i, on);
                    changed = true;
                }
#if GRATIS_PREVIEW
                var itemMin = ImGui.GetItemRectMin(); var itemMax = ImGui.GetItemRectMax();
                TestMultiItems.Add((multi.Id, i, new(itemMin.X, itemMin.Y, itemMax.X, itemMax.Y)));
#endif
                ImGui.PopID();
            }
            ImGui.EndCombo();
        }
        return changed;
    }
}
