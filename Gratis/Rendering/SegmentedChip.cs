using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public readonly record struct ChipProfile {
    public float Height { get; init; }
    public float WidthPadding { get; init; }
    public float IconInset { get; init; }
    public float IconSize { get; init; }
    public float TextInset { get; init; }
    public float FitInset { get; init; }
    public Vector4 Fill { get; init; }
    public float SelectedAlpha { get; init; }
    public Vector4 Ink { get; init; }
}

public static class ChipFaces {
    public static readonly ChipProfile Regular = new() {
        Height = GratisTheme.ControlHeight, WidthPadding = 44, IconInset = 14, IconSize = 13, TextInset = 28,
        Fill = new(1, 1, 1, .035f), SelectedAlpha = .25f, Ink = GratisTheme.Primary,
    };
    public static readonly ChipProfile Compact = Regular with {
        Height = GratisTheme.ControlHeight - 6, WidthPadding = 20, TextInset = 0, FitInset = 12,
    };
}

public readonly record struct ChipOptions(ChipProfile Profile, Vector4 Accent, Vector2 Size,
    float Scale, char Icon = '\0', bool Centered = false, string? Tooltip = null,
    ChipLabelCache? LabelCache = null);

public sealed class ChipLabelCache {
    private string source = "", counted = "";
    private int count = -1;
    private readonly ActionLabelCache fitted = new();

    public string Label(string label, int value) {
        if (source != label || count != value) {
            source = label;
            count = value;
            counted = label + (value > 0 ? $" {value}" : "");
        }
        return counted;
    }

    internal string Fit(string label, float width) => fitted.Fit(label, width);
}

public static class SegmentedChip {
    public static bool Draw(string id, string label, bool selected, in ChipOptions options) {
        var face = options.Profile;
        var s = options.Scale;
        var chosen = UiMotion.Value(ImGui.GetID(id), 6, selected ? 1 : 0);
        ImGui.PushStyleColor(ImGuiCol.Button,
            Vector4.Lerp(face.Fill, options.Accent with { W = face.SelectedAlpha }, chosen));
        try {
            var clicked = ChromeControls.TextButton(id, options.Size);
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var list = ImGui.GetWindowDrawList();
            if (options.Icon != '\0')
                GratisIcons.Draw(list,
                    new Vector2(options.Centered ? (min.X + max.X) / 2 : min.X + face.IconInset * s,
                        (min.Y + max.Y) / 2), options.Icon, face.Ink, face.IconSize * s);
            var text = face.FitInset > 0
                ? (options.LabelCache ?? throw new ArgumentException("Fitted chips need a label cache."))
                    .Fit(label, MathF.Max(1, options.Size.X - face.FitInset * s)) : label;
            var textSize = TextDrawing.Measure(text);
            var position = face.FitInset > 0 ? (min + max - textSize) / 2
                : new Vector2(options.Centered ? (min.X + max.X - textSize.X) / 2 : min.X + face.TextInset * s,
                    (min.Y + max.Y - textSize.Y) / 2);
            TextDrawing.Draw(list, position, ImGui.GetColorU32(face.Ink), text);
            ChromeControls.Tooltip(options.Tooltip ?? label, windowBounds: MapToolbarDrawing.WindowBounds);
            return clicked;
        }
        finally { ImGui.PopStyleColor(); }
    }
}
