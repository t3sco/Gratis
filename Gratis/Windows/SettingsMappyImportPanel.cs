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
/// The "Import from Mappy" block under Settings > Profiles. The button reads the file once, on click; this shows what
/// it found and asks for one confirming click. Nothing here runs unless the user pressed the button.
/// </summary>
internal static class MappyImportPanel {
    private const double NoticeSeconds = 6;
    private const string ConfirmKey = "mappy-import";
    private static readonly ArmedConfirm confirm = new();
    private static MappyImportScan? found;
    private static string notice = "";
    private static double noticeUntil;
    private static int lastDrawnFrame = -10;

    /// <summary>True while the preview is open, so the other import button steps aside.</summary>
    public static bool Open => found != null;

    public static void Reset() {
        found = null;
        notice = "";
        confirm.Clear();
    }

    /// <summary>The button was pressed: read the file now and show what came of it.</summary>
    public static void Scan(SystemConfig config) {
        Show(MappyImport.Scan(config));
    }

    internal static void Show(MappyImportScan scan) {
        confirm.Clear();
        found = null;
        switch (scan.Status) {
            case MappyImportStatus.NotFound:
                Say("No Mappy settings found.");
                break;
            case MappyImportStatus.Unreadable:
                Say("Mappy's settings file could not be read.");
                break;
            default:
                if (scan.Changes == 0)
                    Say("Your settings already match Mappy's.");
                else
                    found = scan;
                break;
        }
    }

    // Test hooks: where the buttons were last drawn, and the armed state.
    internal static Vector4 ButtonBounds, ImportBounds;
    internal static Vector4 ItemBounds() {
        var (min, max) = (ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
        return new(min.X, min.Y, max.X, max.Y);
    }

    internal static void Arm() => confirm.Click(ConfirmKey);
    internal static bool Armed => confirm.IsArmed(ConfirmKey);

    private static void Say(string text) {
        notice = text;
        noticeUntil = ImGui.GetTime() + NoticeSeconds;
    }

    private static string Settings(int count) => count == 1 ? "1 setting" : $"{count} settings";

    /// <summary>Draws the notice or the preview. Returns true when the import changed the settings.</summary>
    public static bool Draw(SystemConfig config, float half, float? scale, Action<string> say) {
        // Leaving the page (another tab, a hidden group) closes the preview and its armed click, so coming back
        // never finds a stale, one-click import waiting.
        var frame = ImGui.GetFrameCount();
        if (frame - lastDrawnFrame > 1 && found != null)
            Reset();
        lastDrawnFrame = frame;
        if (notice.Length > 0 && ImGui.GetTime() > noticeUntil)
            notice = "";
        if (notice.Length > 0)
            ImGui.TextDisabled(notice);
        if (found is not { Data: { } data } scan)
            return false;
        var adjusted = scan.Adjusted > 0 ? $" ({scan.Adjusted} adjusted to fit Gratis's ranges)" : "";
        ImGui.TextWrapped($"Found Mappy settings. {Settings(scan.Changes)} can be imported{adjusted}.");
        ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Secondary);
        if (scan.Groups is { Count: > 0 })
            ImGui.TextWrapped("Includes " + string.Join(", ", scan.Groups) + ".");
        ImGui.TextWrapped("Not imported: window position, per-icon choices, flag history, character details.");
        ImGui.TextWrapped("Gratis is a rework of Mappy by MidoriKami. This copies your Mappy settings once; Mappy is "
            + "not changed or used afterwards.");
        ImGui.PopStyleColor();
        var inner = MathF.Max(1, half - ImGui.GetStyle().FramePadding.X * 2);
        var label = confirm.IsArmed(ConfirmKey)
            ? ChromeControls.FitLabel($"Confirm import {Settings(scan.Changes)}", inner)
            : ChromeControls.FitLabel($"Import {Settings(scan.Changes)}", inner);
        var changed = false;
        var import = ChromeControls.TextButton(label, new Vector2(half, 0), scale: scale);
        ImportBounds = ItemBounds();
        if (import && confirm.Click(ConfirmKey)) {
            var count = MappyImport.Apply(config, data);
            say(count == 0 ? "Your settings already match Mappy's."
                : $"Imported {Settings(count)} from Mappy. Use Undo last applied profile to go back.");
            found = null;
            changed = count > 0;
        }
        ImGui.SameLine();
        if (ChromeControls.TextButton("Cancel", new Vector2(half, 0), scale: scale)) {
            found = null;
            confirm.Clear();
        }
        return changed;
    }
}
