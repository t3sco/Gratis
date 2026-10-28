using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public readonly record struct TabProfile {
    public float Height { get; init; }
    public float WidthInset { get; init; }
    public float Columns { get; init; }
    public float Gap { get; init; }
    public float TextInset { get; init; }
    public Vector2 IconCenter { get; init; }
    public float IconSize { get; init; }
    public Vector4 Ink { get; init; }
    public Vector4 SelectedInk { get; init; }
    public Vector4 Accent { get; init; }
    public Vector4 SelectionFill { get; init; }
    public float SelectionRadius { get; init; }
    public Vector2 IndicatorMin { get; init; }
    public Vector2 IndicatorMax { get; init; }
    public string? SelectionKey { get; init; }
    public bool Compact { get; init; }
}

public static class TabFaces {
    public static readonly TabProfile QuestLog = new() {
        Height = 28, WidthInset = 8, Columns = 3, Gap = 4,
        Ink = GratisTheme.Secondary, SelectedInk = GratisTheme.Primary,
    };
    public static readonly TabProfile Settings = new() {
        Height = 36, WidthInset = 4, Columns = 1, TextInset = 34,
        IconCenter = new(18, 18), IconSize = 16,
        Ink = GratisTheme.Secondary, SelectedInk = GratisTheme.Primary, Accent = GratisTheme.WarmAccent,
        SelectionFill = new(1, 1, 1, .04f), SelectionRadius = GratisTheme.Radius.Control,
        IndicatorMin = new(0, 8), IndicatorMax = new(2, 28), SelectionKey = "##settings-indicator",
    };
    public static readonly TabProfile SettingsCompact = Settings with { Compact = true };
}

public readonly struct TabStrip {
    private readonly TabProfile face;
    private readonly float scale;
    public Vector2 Min { get; }
    public Vector2 Size { get; }
    public bool Clicked { get; }

    public TabStrip(string id, in TabProfile profile, float width, float scale, bool selected = false) {
        face = profile;
        this.scale = scale;
        Min = ImGui.GetCursorScreenPos();
        Size = new((width - face.WidthInset * scale) / face.Columns, face.Height * scale);
        if (face.SelectionKey is null) Clicked = ChromeControls.Button(id, Size);
        else {
            ImGui.PushStyleColor(ImGuiCol.Header, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, Vector4.Zero);
            Clicked = ImGui.Selectable(id, selected, ImGuiSelectableFlags.None, Size);
            ImGui.PopStyleColor(3);
            UiMotion.ItemId(id);
        }
    }

    public void Draw(string label, bool selected, char icon = '\0') {
        var list = ImGui.GetWindowDrawList();
        if (face.SelectionKey is not { } key) {
            ChromeControls.Feedback(list, Min, Min + Size, selected);
            var text = TextDrawing.Measure(label);
            TextDrawing.Draw(list, Min + (Size - text) / 2,
                ImGui.GetColorU32(selected ? face.SelectedInk : face.Ink), label);
            return;
        }
        if (selected) {
            var indicator = UiMotion.Selection(key, Min);
            list.AddRectFilled(indicator, indicator + Size, ImGui.GetColorU32(face.SelectionFill),
                face.SelectionRadius * scale);
            list.AddRectFilled(indicator + face.IndicatorMin * scale, indicator + face.IndicatorMax * scale,
                ImGui.GetColorU32(face.Accent), GratisTheme.Radius.Pill((face.IndicatorMax - face.IndicatorMin) * scale));
        }
        ChromeControls.Feedback(list, Min, Min + Size, fill: true, scale: scale);
        GratisIcons.Draw(list, Min + face.IconCenter * scale, icon,
            selected ? face.Accent : face.Ink, face.IconSize * scale);
        if (face.Compact) ChromeControls.Tooltip(label, scale: scale);
        else list.AddText(Min + new Vector2(face.TextInset * scale, (Size.Y - ImGui.GetFontSize()) / 2),
            ImGui.GetColorU32(selected ? face.SelectedInk : face.Ink), label);
    }
}
