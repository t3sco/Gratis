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

public static class SettingsProfilesDrawing {
    private static SettingsProfileSet? owner;
    private static int selected;
    private static string name = "";
    private static string message = "";
    private static bool deleteArmed;
    private static ProfileData? incoming;
    private static int incomingKnown;
    private static string incomingName = "";

    public static bool Draw(SystemConfig config) {
        var set = config.ProfileOptions;
        var scale = config.IsGratis ? SettingsTheme.Scale : (float?)null;
        var changed = false;
        if (!ReferenceEquals(owner, set)) {
            owner = set;
            selected = 0;
            message = "";
            incoming = null;
            deleteArmed = false;
            name = "";
        }
        if (set.Find(selected) == null) {
            selected = set.Profiles.Count > 0 ? set.Profiles[0].Slot : 0;
            name = set.Find(selected)?.Name ?? "";
            deleteArmed = false;
        }
        var width = MathF.Max(1, ImGui.GetContentRegionAvail().X);
        var half = MathF.Max(1, (width - ImGui.GetStyle().ItemSpacing.X) / 2);
        var lineUsed = 0f;
        foreach (var profile in set.Profiles) {
            var chipWidth = MathF.Min(width, ImGui.CalcTextSize(profile.Name).X + ImGui.GetStyle().FramePadding.X * 2);
            var label = ChromeControls.FitLabel(profile.Name,
                MathF.Max(1, chipWidth - ImGui.GetStyle().FramePadding.X * 2));
            if (lineUsed > 0 && lineUsed + ImGui.GetStyle().ItemSpacing.X + chipWidth <= width) {
                ImGui.SameLine();
                lineUsed += ImGui.GetStyle().ItemSpacing.X + chipWidth;
            }
            else
                lineUsed = chipWidth;
            ImGui.PushID(profile.Slot);
            ImGui.PushStyleColor(ImGuiCol.Button,
                selected == profile.Slot ? GratisTheme.WarmAccent with { W = .2f } : GratisTheme.Surface);
            var start = ImGui.GetCursorScreenPos();
            var height = ImGui.GetFrameHeight();
            if (ChromeControls.TextButton("##profile-chip", new Vector2(chipWidth, height), scale: scale)) {
                selected = profile.Slot;
                name = profile.Name;
                deleteArmed = false;
            }
            ImGui.GetWindowDrawList().AddText(start
                + new Vector2(ImGui.GetStyle().FramePadding.X, (height - ImGui.GetFontSize()) / 2),
                ImGui.GetColorU32(GratisTheme.Primary), label);
            ImGui.PopStyleColor();
            ChromeControls.Tooltip(profile.Name, scale: scale);
            ImGui.PopID();
        }
        if (set.Profiles.Count == 0)
            ImGui.TextDisabled("No profiles yet.");
        ImGui.BeginDisabled(set.FreeSlot() == 0);
        if (ChromeControls.TextButton("Save current settings as profile", new Vector2(width, 0), scale: scale)) {
            var slot = ProfileManager.Save(config, "");
            selected = slot;
            name = set.Find(slot)?.Name ?? "";
            message = "Saved " + name + ".";
            deleteArmed = false;
            changed = true;
        }
        ImGui.EndDisabled();
        if (set.FreeSlot() == 0)
            ImGui.TextDisabled($"All {SettingsProfileSet.Limit} profile slots are used. Delete one to save another.");
        if (set.Find(selected) is { } profile2) {
            ImGui.SetNextItemWidth(width);
            ImGui.InputText("##profile-name", ref name, 80);
            if (ChromeControls.TextButton("Apply profile", new Vector2(half, 0), scale: scale)) {
                var count = ProfileManager.Apply(config, selected);
                message = count < 0 ? "That profile could not be read."
                    : count == 0 ? "Already matches your settings." : $"Applied {profile2.Name}: {count} settings changed.";
                deleteArmed = false;
                changed |= count > 0;
            }
            ImGui.SameLine();
            if (ChromeControls.TextButton("Copy as text", new Vector2(half, 0), scale: scale)) {
                ImGui.SetClipboardText(ProfileManager.Export(config, selected));
                message = "Copied. Paste it anywhere to share this profile.";
            }
            if (ChromeControls.TextButton("Rename", new Vector2(half, 0), scale: scale)) {
                ProfileManager.Rename(config, selected, name);
                name = set.Find(selected)?.Name ?? "";
                message = "Renamed.";
                changed = true;
            }
            ImGui.SameLine();
            if (ChromeControls.TextButton(deleteArmed ? "Confirm delete" : "Delete", new Vector2(half, 0),
                scale: scale)) {
                if (deleteArmed) {
                    ProfileManager.Delete(config, selected);
                    message = "Deleted.";
                    changed = true;
                }
                deleteArmed = !deleteArmed;
            }
        }
        if (incoming == null) {
            if (ChromeControls.TextButton("Import from clipboard", new Vector2(width, 0), scale: scale)) {
                if (ProfileCodec.TryDecode(ImGui.GetClipboardText(), out var data, out var error)) {
                    incoming = data;
                    incomingName = data!.Name;
                    incomingKnown = ProfileCodec.Recognised(data);
                    message = "";
                }
                else
                    message = error;
            }
        }
        else {
            ImGui.TextWrapped($"Import {incomingKnown} settings from this profile? It is saved to a slot; nothing "
                + "changes until you apply it.");
            ImGui.SetNextItemWidth(width);
            ImGui.InputText("##profile-import-name", ref incomingName, 80);
            ImGui.BeginDisabled(set.FreeSlot() == 0);
            if (ChromeControls.TextButton("Save imported profile", new Vector2(half, 0), scale: scale)) {
                var slot = ProfileManager.Import(config, incoming, incomingName);
                selected = slot;
                name = set.Find(slot)?.Name ?? "";
                message = "Imported " + name + ". Apply it when you are ready.";
                incoming = null;
                deleteArmed = false;
                changed = true;
            }
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (ChromeControls.TextButton("Cancel", new Vector2(half, 0), scale: scale))
                incoming = null;
            if (set.FreeSlot() == 0)
                ImGui.TextDisabled("Delete a profile first to make room.");
        }
        ImGui.BeginDisabled(set.Undo.Length == 0);
        if (ChromeControls.TextButton("Undo last applied profile", new Vector2(width, 0), scale: scale)) {
            message = ProfileManager.Undo(config) ? "Restored your settings from before." : "Nothing to undo.";
            changed = true;
        }
        ImGui.EndDisabled();
        if (message.Length > 0) {
            ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Secondary);
            ImGui.TextWrapped(message);
            ImGui.PopStyleColor();
        }
        return changed;
    }
}
