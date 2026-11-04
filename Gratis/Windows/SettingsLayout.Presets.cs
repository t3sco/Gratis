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
    /// <summary>Up to this many choices draw as chips; more draw as a dropdown.</summary>
    internal const int MaxChipChoices = 4;
    private const string CustomLabel = "Custom";

    private sealed class PresetControls(SettingsPreset preset) {
        public readonly ChipLabelCache[] Labels = Array.ConvertAll(preset.ChoiceLabels, _ => new ChipLabelCache());
        public readonly string[] Names = [.. preset.ChoiceLabels, CustomLabel];
    }

    private readonly Dictionary<string, PresetControls> presetControls = [];
#if GRATIS_PREVIEW
    public readonly List<(string Preset, int Choice, Vector4 Bounds)> TestPresetChips = [];
    public readonly List<(string Preset, Vector4 Label, Vector4 Control, bool Custom)> TestPresetRows = [];
#endif

    /// <summary>A preset as a row of the settings table: its label, a Custom note when no choice matches, then the control.</summary>
    private bool DrawPresetRow(SystemConfig config, SettingsPreset preset, float s, bool stacked, bool essentials) {
        ImGui.PushID(preset.Id);
        ImGui.TableNextRow(0, RowHeight(essentials, s));
        ImGui.TableNextColumn();
        // Chips are shorter than the row, so the label and chips share one inset instead of hugging the card's top edge.
        var rowTop = ImGui.GetCursorPosY() + MathF.Max(0, (RowHeight(false, s) - ChipFaces.Compact.Height * s) / 2);
        ImGui.SetCursorPosY(rowTop);
        ImGui.AlignTextToFramePadding();
        var cellStart = ImGui.GetCursorScreenPos();
        var cellWidth = ImGui.GetContentRegionAvail().X;
        var current = preset.Current(config);
        if (current == SettingsPreset.Custom) {
            using var font = SettingsTheme.Font(12);
            var width = ImGui.CalcTextSize(CustomLabel).X;
            ImGui.SetCursorScreenPos(new(cellStart.X + MathF.Max(0, cellWidth - width), cellStart.Y));
            ImGui.TextColored(GratisTheme.Secondary, CustomLabel);
            ImGui.SetCursorScreenPos(cellStart);
        }
        // The Custom note above uses up the alignment, so without this a Custom row's label sits higher than its neighbours'.
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(preset.Label);
#if GRATIS_PREVIEW
        var labelMin = ImGui.GetItemRectMin(); var labelMax = ImGui.GetItemRectMax();
#endif
        if (essentials)
            DrawHelperLine("preset-" + preset.Id, preset.Tooltip, cellWidth, rowTop, s);
        else
            ChromeControls.Tooltip(preset.Tooltip, scale: s);
        if (!stacked)
            ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        ImGui.SetCursorPosY(rowTop);
        ImGui.BeginGroup();
        var changed = DrawPresetChoices(config, preset, current, s);
        ImGui.EndGroup();
#if GRATIS_PREVIEW
        var controlMin = ImGui.GetItemRectMin(); var controlMax = ImGui.GetItemRectMax();
        TestPresetRows.Add((preset.Id, new(labelMin.X, labelMin.Y, labelMax.X, labelMax.Y),
            new(controlMin.X, controlMin.Y, controlMax.X, controlMax.Y), current == SettingsPreset.Custom));
#endif
        ImGui.PopID();
        return changed;
    }

    /// <summary>Chips sized to their labels and stretched to the row, or a dropdown past four choices.</summary>
    private bool DrawPresetChoices(SystemConfig config, SettingsPreset preset, int current, float scale) {
        if (!presetControls.TryGetValue(preset.Id, out var controls))
            presetControls.Add(preset.Id, controls = new(preset));
        var count = preset.ChoiceCount;
        if (count > MaxChipChoices) {
            var selected = current == SettingsPreset.Custom ? count : current;
            if (!ChromeControls.Combo("##preset", ref selected, controls.Names, scale) || selected >= count
                || selected == current)
                return false;
            preset.Apply(config, selected);
            return true;
        }
        using var font = SettingsTheme.Font(GratisTheme.TextSize, ChromeFontRole.Chip);
        var gap = GratisTheme.Grid * scale;
        var height = ChipFaces.Compact.Height * scale;
        Span<float> widths = stackalloc float[MaxChipChoices];
        var natural = 0f;
        for (var i = 0; i < count; i++)
            natural += widths[i] = TextDrawing.Measure(preset.ChoiceLabels[i]).X + ChipFaces.Compact.WidthPadding * scale;
        var stretch = MathF.Max(1, ImGui.GetContentRegionAvail().X - gap * (count - 1)) / natural;
        var changed = false;
        for (var i = 0; i < count; i++) {
            if (i > 0)
                ImGui.SameLine(0, gap);
            ImGui.PushID(i);
            var clicked = SegmentedChip.Draw("##preset-choice", preset.ChoiceLabels[i], i == current,
                new(ChipFaces.Compact, GratisTheme.WarmAccent, new Vector2(widths[i] * stretch, height), scale,
                    Tooltip: preset.ChoiceTips[i], LabelCache: controls.Labels[i]));
#if GRATIS_PREVIEW
            var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax();
            TestPresetChips.Add((preset.Id, i, new(min.X, min.Y, max.X, max.Y)));
#endif
            ImGui.PopID();
            if (clicked && i != current) {
                preset.Apply(config, i);
                changed = true;
            }
        }
        return changed;
    }
}
