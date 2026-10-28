using System.Numerics;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class EmptyStateVerification {
    public static void Run() {
        EmptyProfile[] profiles = [EmptyFaces.PaletteSearch, EmptyFaces.PaletteRecent, EmptyFaces.PaletteBrowser,
            EmptyFaces.Location, EmptyFaces.Quest, EmptyFaces.QuestHelper, EmptyFaces.ClassicSettings, EmptyFaces.SettingsHelper];
        var grey = new Vector4(143 / 255f, 143 / 255f, 143 / 255f, 1);
        EmptyProfile[] expected = [new(EmptyText.SecondaryWrapped, Spacing: true, Ink: grey, Helper: "Try a zone name or choose another filter."),
            new(EmptyText.SecondaryWrapped, 12, Ink: grey), new(EmptyText.SecondaryWrapped, Ink: grey), new(EmptyText.SecondaryWrapped, Ink: grey),
            new(EmptyText.SecondaryWrapped, Ink: grey), new(EmptyText.SecondaryWrapped, Ink: grey), new(EmptyText.SecondaryWrapped, Ink: grey),
            new(EmptyText.SecondaryWrapped, 12, Ink: grey)];
        if (!profiles.SequenceEqual(expected)) throw new InvalidOperationException("Empty-state profile literals changed.");
        string[] labels = ["No places found", "Your next destination starts a new journey.", "No charted places to browse.",
            "No zones found", "No quests match your search.", "Enlarge the map to read quests.",
            "No settings match. Try another name or change the visual style.", "A helper message long enough to wrap within the current content region."];
        foreach (var style in new[] { 0, 1 })
            foreach (var scale in new[] { 1f, 1.25f })
                SectionHeaderVerification.VerifyDraw("Empty states", style, scale, profiles.Length, (original, i) => {
                    if (!original) { EmptyState.Draw(labels[i], profiles[i], scale); return; }
                    if (i == 0) ImGui.Spacing();
                    using var font = i is 1 or 7 ? MapDrawContext.Current.GetChromeFont(12 * scale, ChromeFontRole.Body)?.Push() : null;
                    ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Secondary);
                    ImGui.TextWrapped(labels[i]);
                    if (i == 0) ImGui.TextWrapped("Try a zone name or choose another filter.");
                    ImGui.PopStyleColor();
                });
    }
}
