using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public enum EmptyText { Plain, Disabled, Wrapped, SecondaryWrapped }
public readonly record struct EmptyProfile(EmptyText Text, float FontSize = 0,
    ChromeFontRole FontRole = ChromeFontRole.Body, bool Spacing = false, string? Helper = null, Vector4 Ink = default);

public static class EmptyFaces {
    public static readonly EmptyProfile PaletteSearch = new(EmptyText.SecondaryWrapped, Spacing: true, Ink: GratisTheme.Secondary,
        Helper: "Try a zone name or choose another filter.");
    public static readonly EmptyProfile PaletteRecent = new(EmptyText.SecondaryWrapped, 12, Ink: GratisTheme.Secondary);
    public static readonly EmptyProfile PaletteBrowser = new(EmptyText.SecondaryWrapped, Ink: GratisTheme.Secondary);
    public static readonly EmptyProfile Location = new(EmptyText.SecondaryWrapped, Ink: GratisTheme.Secondary);
    public static readonly EmptyProfile Quest = new(EmptyText.SecondaryWrapped, Ink: GratisTheme.Secondary);
    public static readonly EmptyProfile QuestHelper = new(EmptyText.SecondaryWrapped, Ink: GratisTheme.Secondary);
    public static readonly EmptyProfile ClassicSettings = new(EmptyText.SecondaryWrapped, Ink: GratisTheme.Secondary);
    public static readonly EmptyProfile SettingsHelper = new(EmptyText.SecondaryWrapped, 12, Ink: GratisTheme.Secondary);
}

public static class EmptyState {
    public static void Draw(string text, in EmptyProfile face, float scale = 1) {
        if (face.Spacing) ImGui.Spacing();
        using var font = face.FontSize > 0
            ? MapDrawContext.Current.GetChromeFont(face.FontSize * scale, face.FontRole)?.Push() : null;
        switch (face.Text) {
            case EmptyText.Plain: ImGui.TextUnformatted(text); break;
            case EmptyText.Disabled: ImGui.TextDisabled(text); break;
            case EmptyText.Wrapped: ImGui.TextWrapped(text); break;
            case EmptyText.SecondaryWrapped:
                ImGui.PushStyleColor(ImGuiCol.Text, face.Ink);
                ImGui.TextWrapped(text);
                ImGui.PopStyleColor();
                break;
        }
        if (face.Helper is { } helper) {
            ImGui.PushStyleColor(ImGuiCol.Text, face.Ink);
            ImGui.TextWrapped(helper);
            ImGui.PopStyleColor();
        }
    }
}
