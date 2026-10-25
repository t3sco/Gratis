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

public static class SettingsWindowPresets
{
    private static WindowPreset? editing;
    private static string name = "";
    public static bool Draw(SystemConfig config)
    {
        var settings = config.WindowPresetOptions;
        var controller = MapDrawContext.Current.WindowPresets;
        var changed = false;
        if (editing == null || settings.Find(editing.Slot) != editing) {
            editing = settings.Presets.Count > 0 ? settings.Presets[0] : null;
            name = editing?.Name ?? "";
        }
        var width = MathF.Max(1, ImGui.GetContentRegionAvail().X);
        ImGui.TextUnformatted("Open map at");
        changed |= Choice("##window-open-at", settings, ref settings.OpenAt, "Last position (default)", width);
        ImGui.TextUnformatted("In duties / instances");
        changed |= Choice("##window-duty-at", settings, ref settings.DutyOpenAt, "Use Open map at (default)", width);
        var lineUsed = 0f;
        foreach (var preset in settings.Presets) {
            var chipWidth = MathF.Min(width, ImGui.CalcTextSize(preset.Name).X + ImGui.GetStyle().FramePadding.X * 2);
            var label = ChromeControls.FitLabel(preset.Name, MathF.Max(1, chipWidth - ImGui.GetStyle().FramePadding.X * 2));
            if (lineUsed > 0 && lineUsed + ImGui.GetStyle().ItemSpacing.X + chipWidth <= width) {
                ImGui.SameLine(); lineUsed += ImGui.GetStyle().ItemSpacing.X + chipWidth;
            }
            else lineUsed = chipWidth;
            ImGui.PushID(preset.Slot);
            ImGui.PushStyleColor(ImGuiCol.Button, ReferenceEquals(editing, preset) ? GratisTheme.WarmAccent with { W = .2f } : GratisTheme.Surface);
            var start = ImGui.GetCursorScreenPos();
            var height = ImGui.GetFrameHeight();
            if (ImGui.Button("##preset-chip", new Vector2(chipWidth, height))) { editing = preset; name = preset.Name; }
            ImGui.GetWindowDrawList().AddText(start + new Vector2(ImGui.GetStyle().FramePadding.X, (height - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(GratisTheme.Primary), label);
            ImGui.PopStyleColor();
            ChromeControls.Tooltip(preset.Name);
            ImGui.PopID();
        }
        ImGui.BeginDisabled(settings.Presets.Count >= WindowPresetSettings.Limit);
        if (ImGui.Button("Save current as preset", new Vector2(width, 0))) changed |= controller.SaveCurrent(config);
        ImGui.EndDisabled();
        if (editing != null && settings.Find(editing.Slot) == editing) {
            ImGui.SetNextItemWidth(width);
            ImGui.InputText("##window-preset-name", ref name, 80);
            if (ImGui.Button("Apply preset", new Vector2(width, 0))) controller.Request(editing.Slot);
            var half = MathF.Max(1, (width - ImGui.GetStyle().ItemSpacing.X) / 2);
            if (ImGui.Button("Rename", new Vector2(half, 0))) {
                editing.Name = WindowPresetSettings.CleanName(name, editing.Slot); name = editing.Name; changed = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Delete", new Vector2(half, 0))) { changed |= settings.Delete(editing.Slot); editing = null; }
        }
        if (ImGui.Button("Reset window presets", new Vector2(width, 0))) {
            config.WindowPresets = new(); editing = null; controller.Cancel(); changed = true;
        }
        if (changed) controller.Changed();
        return changed;
    }
    private static bool Choice(string id, WindowPresetSettings settings, ref int slot, string fallback, float width)
    {
        var changed = false;
        ImGui.SetNextItemWidth(width);
        if (ImGui.BeginCombo(id, settings.Find(slot)?.Name ?? fallback)) {
            if (ImGui.Selectable(fallback, slot == 0)) { slot = 0; changed = true; }
            foreach (var preset in settings.Presets) {
                ImGui.PushID(preset.Slot);
                if (ImGui.Selectable(preset.Name + "###choice", slot == preset.Slot)) { slot = preset.Slot; changed = true; }
                ImGui.PopID();
            }
            ImGui.EndCombo();
        }
        return changed;
    }
}
