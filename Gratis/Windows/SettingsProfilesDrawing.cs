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
    private const double MessageSeconds = 6;
    private static SettingsProfileSet? owner;
    private static int selected;
    private static string name = "";
    private static string message = "";
    private static double messageUntil;
    private static bool revertArmed;
    private static readonly ArmedConfirm deleteConfirm = new();
    private static ProfileData? incoming;
    private static int incomingKnown;
    private static string incomingName = "";

    /// <summary>Says something the screen does not already show; it fades after a few seconds.</summary>
    private static void Say(string text) {
        message = text;
        messageUntil = ImGui.GetTime() + MessageSeconds;
    }

    private static void Select(SettingsProfileSet set, int slot) {
        selected = slot;
        name = set.Find(slot)?.Name ?? "";
        message = "";
        deleteConfirm.Clear();
    }

    public static bool Draw(SystemConfig config) {
        var set = config.ProfileOptions;
        var scale = config.IsGratis ? SettingsTheme.Scale : (float?)null;
        var changed = false;
        if (!ReferenceEquals(owner, set)) {
            owner = set;
            selected = 0;
            message = "";
            incoming = null;
            MappyImportPanel.Reset();
            revertArmed = false;
            deleteConfirm.Clear();
            name = "";
        }
        if (set.Find(selected) == null)
            Select(set, set.Profiles.Count > 0 ? set.Profiles[0].Slot : 0);
        if (message.Length > 0 && ImGui.GetTime() > messageUntil)
            message = "";
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
            if (ChromeControls.TextButton("##profile-chip", new Vector2(chipWidth, height), scale: scale))
                Select(set, profile.Slot);
            ImGui.GetWindowDrawList().AddText(start
                + new Vector2(ImGui.GetStyle().FramePadding.X, (height - ImGui.GetFontSize()) / 2),
                ImGui.GetColorU32(GratisTheme.Primary), label);
            ImGui.PopStyleColor();
            if (label != profile.Name)
                ChromeControls.Tooltip(profile.Name, scale: scale);
            ImGui.PopID();
        }
        if (set.Profiles.Count == 0)
            ImGui.TextDisabled("No profiles yet.");
        ImGui.BeginDisabled(set.FreeSlot() == 0);
        if (ChromeControls.TextButton("Save current settings as profile", new Vector2(width, 0), scale: scale)) {
            Select(set, ProfileManager.Save(config, ""));
            changed = true;
        }
        ImGui.EndDisabled();
        if (set.FreeSlot() == 0)
            ImGui.TextDisabled($"All {SettingsProfileSet.Limit} profile slots are used. Delete one to save another.");
        if (set.Find(selected) is { } profile2) {
            // The box holds the selected profile's name. Enter, or leaving the box, keeps the new name.
            ImGui.SetNextItemWidth(width);
            ImGui.InputText("##profile-name", ref name, 80, ImGuiInputTextFlags.EnterReturnsTrue);
            if (ImGui.IsItemDeactivatedAfterEdit()) {
                if (name.Trim().Length > 0 && name != profile2.Name && ProfileManager.Rename(config, selected, name))
                    changed = true;
                name = set.Find(selected)?.Name ?? "";
            }
            if (ChromeControls.TextButton("Apply profile", new Vector2(half, 0), scale: scale)) {
                var count = ProfileManager.Apply(config, selected);
                Say(count < 0 ? "That profile could not be read."
                    : count == 0 ? "Already matches your settings." : $"Applied {profile2.Name}: {count} settings changed.");
                deleteConfirm.Clear();
                changed |= count > 0;
            }
            ImGui.SameLine();
            var deleteKey = "delete:" + selected;
            var deleteLabel = deleteConfirm.IsArmed(deleteKey)
                ? ChromeControls.FitLabel("Confirm delete " + profile2.Name,
                    MathF.Max(1, half - ImGui.GetStyle().FramePadding.X * 2))
                : "Delete";
            if (ChromeControls.TextButton(deleteLabel, new Vector2(half, 0), scale: scale)
                && deleteConfirm.Click(deleteKey)) {
                ProfileManager.Delete(config, selected);
                changed = true;
            }
        }
        ImGui.BeginDisabled(set.Undo.Length == 0);
        if (ChromeControls.TextButton("Undo last applied profile", new Vector2(width, 0), scale: scale)) {
            Say(ProfileManager.Undo(config) ? "Restored your settings from before." : "Nothing to undo.");
            changed = true;
        }
        ImGui.EndDisabled();
        if (revertArmed && !ProfileManager.HasUnsavedChanges(config))
            revertArmed = false;
        // The rarely used actions share one quiet row. Import and revert open their own block under it, so the
        // buttons above never move.
        var quietUsed = 0f;
        var gap = ImGui.GetStyle().ItemSpacing.X;
        Vector2 QuietSize(string label) {
            var w = MathF.Min(width, ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 2
                + 16 * (scale ?? 1));
            if (quietUsed > 0 && quietUsed + gap + w <= width) {
                ImGui.SameLine();
                quietUsed += gap + w;
            }
            else
                quietUsed = w;
            return new Vector2(w, 0);
        }
        if (set.Find(selected) != null
            && ChromeControls.TextButton("Copy as text", QuietSize("Copy as text"), scale: scale)) {
            ImGui.SetClipboardText(ProfileManager.Export(config, selected));
            Say("Copied. Paste it anywhere to share this profile.");
        }
        if (incoming == null && !MappyImportPanel.Open
            && ChromeControls.TextButton("Import from clipboard", QuietSize("Import from clipboard"), scale: scale)) {
            if (ProfileCodec.TryDecode(ImGui.GetClipboardText(), out var data, out var error)) {
                incoming = data;
                incomingName = data!.Name;
                incomingKnown = ProfileCodec.Recognised(data);
                message = "";
            }
            else
                Say(error);
        }
        if (incoming == null && !MappyImportPanel.Open) {
            var clicked = ChromeControls.TextButton("Import from Mappy", QuietSize("Import from Mappy"), scale: scale);
            MappyImportPanel.ButtonBounds = MappyImportPanel.ItemBounds();
            if (clicked) {
                message = "";
                MappyImportPanel.Scan(config);
            }
        }
        if (!revertArmed
            && ChromeControls.TextButton("Revert to defaults", QuietSize("Revert to defaults"), scale: scale)) {
            if (ProfileManager.HasUnsavedChanges(config)) {
                revertArmed = true;
                message = "";
            }
            else {
                var count = ProfileManager.RevertToDefaults(config);
                Say(count == 0 ? "Already on the defaults." : $"Reverted {count} settings to the defaults.");
                changed |= count > 0;
            }
        }
        if (incoming != null) {
            ImGui.TextWrapped($"Import {incomingKnown} settings from this profile? It is saved to a slot; nothing "
                + "changes until you apply it.");
            ImGui.SetNextItemWidth(width);
            ImGui.InputText("##profile-import-name", ref incomingName, 80);
            ImGui.BeginDisabled(set.FreeSlot() == 0);
            if (ChromeControls.TextButton("Save imported profile", new Vector2(half, 0), scale: scale)) {
                var slot = ProfileManager.Import(config, incoming, incomingName);
                Select(set, slot);
                Say("Imported " + name + ". Apply it when you are ready.");
                incoming = null;
                changed = true;
            }
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (ChromeControls.TextButton("Cancel", new Vector2(half, 0), scale: scale))
                incoming = null;
            if (set.FreeSlot() == 0)
                ImGui.TextDisabled("Delete a profile first to make room.");
        }
        changed |= MappyImportPanel.Draw(config, half, scale, Say);
        if (revertArmed) {
            ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.WarmAccent);
            ImGui.TextWrapped("Your current settings are not saved as a profile. Reverting replaces them. You can "
                + "undo once afterwards.");
            ImGui.PopStyleColor();
            ImGui.BeginDisabled(set.FreeSlot() == 0);
            if (ChromeControls.TextButton("Save, then revert", new Vector2(width, 0), scale: scale)) {
                Select(set, ProfileManager.Save(config, ""));
                var count = ProfileManager.RevertToDefaults(config);
                Say($"Saved {name}, then reverted {count} settings to the defaults.");
                revertArmed = false;
                changed = true;
            }
            ImGui.EndDisabled();
            if (ChromeControls.TextButton("Revert anyway", new Vector2(half, 0), scale: scale)) {
                var count = ProfileManager.RevertToDefaults(config);
                Say($"Reverted {count} settings to the defaults.");
                revertArmed = false;
                changed |= count > 0;
            }
            ImGui.SameLine();
            if (ChromeControls.TextButton("Cancel", new Vector2(half, 0), scale: scale))
                revertArmed = false;
            if (set.FreeSlot() == 0)
                ImGui.TextDisabled("All profile slots are used, so it cannot be saved first.");
        }
        if (message.Length > 0) {
            ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Secondary);
            ImGui.TextWrapped(message);
            ImGui.PopStyleColor();
        }
        return changed;
    }
}
