using System.Numerics;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class EmptyStateVerification {
    public static void Run() {
        EmptyProfile[] profiles = [EmptyFaces.PaletteSearch, EmptyFaces.PaletteRecent, EmptyFaces.PaletteBrowser,
            EmptyFaces.Location, EmptyFaces.Quest, EmptyFaces.QuestHelper, EmptyFaces.ClassicSettings, EmptyFaces.SettingsHelper];
        EmptyProfile[] expected = [new(EmptyText.Plain, Spacing: true, Helper: "Try a zone name or choose another filter."),
            new(EmptyText.Disabled, 12), new(EmptyText.Wrapped), new(EmptyText.Disabled),
            new(EmptyText.Wrapped), new(EmptyText.Wrapped), new(EmptyText.Disabled), new(EmptyText.SecondaryWrapped, 12, Ink: new Vector4(143 / 255f, 143 / 255f, 143 / 255f, 1))];
        if (!profiles.SequenceEqual(expected)) throw new InvalidOperationException("Empty-state profile literals changed.");
        string[] labels = ["No places found", "Your next destination starts a new journey.", "No charted places to browse.",
            "No zones found", "No quests match your search.", "Enlarge the map to read quests.",
            "No settings match. Try another name or change the visual style.", "A helper message long enough to wrap within the current content region."];
        foreach (var style in new[] { 0, 1 })
            foreach (var scale in new[] { 1f, 1.25f })
                SectionHeaderVerification.VerifyDraw("Empty states", style, scale, profiles.Length, (original, i) => {
                    if (!original) { EmptyState.Draw(labels[i], profiles[i], scale); return; }
                    switch (i) {
                        case 0:
                            ImGui.Spacing();
                            ImGui.TextUnformatted(labels[i]);
                            ImGui.TextWrapped("Try a zone name or choose another filter.");
                            break;
                        case 1:
                            using (MapDrawContext.Current.GetChromeFont(12 * scale, ChromeFontRole.Body)?.Push())
                                ImGui.TextDisabled(labels[i]);
                            break;
                        case 3: case 6: ImGui.TextDisabled(labels[i]); break;
                        case 7:
                            using (MapDrawContext.Current.GetChromeFont(12 * scale, ChromeFontRole.Body)?.Push()) {
                                ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Secondary);
                                ImGui.TextWrapped(labels[i]);
                                ImGui.PopStyleColor();
                            }
                            break;
                        default: ImGui.TextWrapped(labels[i]); break;
                    }
                });
    }
}
