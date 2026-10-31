using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public readonly record struct RowProfile {
    public float Height { get; init; }
    public float MinimumWidth { get; init; }
    public float TextInset { get; init; }
    public float TrailingInset { get; init; }
    public float IconInset { get; init; }
    public float IconSize { get; init; }
    public float ChildReserve { get; init; }
    public float ChildInset { get; init; }
    public Vector2 ChildHalfSize { get; init; }
    public Vector4 Ink { get; init; }
    public Vector4 SecondaryInk { get; init; }
    public Vector4 SelectionFill { get; init; }
    public string? SelectionKey { get; init; }
    public bool Fit { get; init; }
    public bool ClampFit { get; init; }
    public bool TextDrawing { get; init; }
    public bool Control { get; init; }
    public bool DisabledScope { get; init; }
    public bool TruncatedTooltip { get; init; }
}

public static class RowFaces {
    public static readonly RowProfile Location = new() {
        Height = GratisTheme.ControlHeight, MinimumWidth = 1, TextInset = 8, TrailingInset = 8,
        Ink = GratisTheme.Primary, Fit = true,
    };
    public static readonly RowProfile Navigation = new() {
        Height = GratisTheme.ControlHeight, MinimumWidth = 200, TextInset = 36,
        IconInset = 16, IconSize = GratisTheme.IconSize,
        Ink = GratisTheme.Primary, SecondaryInk = GratisTheme.Secondary,
    };
    public static readonly RowProfile Reward = new() {
        Height = 36, TextInset = 8, Ink = GratisTheme.Primary, SecondaryInk = GratisTheme.Secondary, TextDrawing = true,
    };
    public static readonly RowProfile Context = Location with {
        TextInset = 40, IconInset = 18, IconSize = GratisTheme.IconSize,
        ChildReserve = 24, ChildInset = 16, ChildHalfSize = new(2, 4),
        SecondaryInk = GratisTheme.Secondary, SelectionFill = GratisTheme.WarmAccent with { W = .08f },
        SelectionKey = "##context-highlight", ClampFit = true, Control = true, DisabledScope = true,
        TruncatedTooltip = true,
    };
}

public readonly record struct RowOptions(RowProfile Profile, float Scale, float Width = 0,
    char Icon = '\0', bool Enabled = true, bool Children = false);

public sealed class MenuRowCache(string id) {
    public string Id { get; } = id;
    internal ActionLabelCache Label { get; } = new();
}

public readonly struct MenuRow {
    private readonly MenuRowCache cache;
    private readonly RowOptions options;
    public Vector2 Min { get; }
    public Vector2 Size { get; }
    public bool Clicked { get; }
    public bool Hovered { get; }

    public MenuRow(MenuRowCache cache, in RowOptions options) {
        this.cache = cache;
        this.options = options;
        var face = options.Profile;
        Min = ImGui.GetCursorScreenPos();
        Size = new(options.Width != 0 ? options.Width
            : MathF.Max(face.MinimumWidth == 1 ? 1 : face.MinimumWidth * options.Scale,
                ImGui.GetContentRegionAvail().X), face.Height * options.Scale);
        if (face.DisabledScope) ImGui.BeginDisabled(!options.Enabled);
        Clicked = ChromeControls.Button(cache.Id, Size);
        if (face.DisabledScope) ImGui.EndDisabled();
        Hovered = ImGui.IsItemHovered();
    }

    public void Draw(string label, bool selected = false) {
        var face = options.Profile;
        var s = options.Scale;
        var list = ImGui.GetWindowDrawList();
        using var control = face.Control ? UiMotion.Control(list, Min, Min + Size) : (UiMotion.VisualScope?)null;
        selected &= options.Enabled;
        if (selected && face.SelectionKey is { } key) {
            var highlight = UiMotion.Selection(key, Min);
            list.AddRectFilled(highlight, highlight + Size, ImGui.GetColorU32(face.SelectionFill),
                GratisTheme.Radius.Control * s);
        }
        ChromeControls.Feedback(list, Min, Min + Size, selected);
        if (face.IconSize > 0)
            GratisIcons.Draw(list, Min + new Vector2(face.IconInset * s, Size.Y / 2), options.Icon,
                face.SecondaryInk, face.IconSize * s);
        var reserve = options.Children ? face.ChildReserve * s : face.TrailingInset * s;
        var displayed = face.Fit ? cache.Label.Fit(label, face.ClampFit
            ? MathF.Max(0, Size.X - face.TextInset * s - reserve)
            : Size.X - (face.TextInset + face.TrailingInset) * s) : label;
        var position = Min + new Vector2(face.TextInset * s,
            (Size.Y - (face.TextDrawing ? ImGui.GetTextLineHeight() : ImGui.GetFontSize())) / 2);
        var ink = ImGui.GetColorU32(options.Enabled ? face.Ink : face.SecondaryInk);
        if (face.TextDrawing) TextDrawing.Draw(list, position, ink, displayed);
        else list.AddText(position, ink, displayed);
        if (options.Children) {
            var center = Min + new Vector2(Size.X - face.ChildInset * s, Size.Y / 2);
            var half = face.ChildHalfSize;
            list.AddLine(center + new Vector2(-half.X, -half.Y) * s, center + new Vector2(half.X, 0) * s,
                ImGui.GetColorU32(face.SecondaryInk), s);
            list.AddLine(center + new Vector2(half.X, 0) * s, center + new Vector2(-half.X, half.Y) * s,
                ImGui.GetColorU32(face.SecondaryInk), s);
        }
        if (face.TruncatedTooltip && displayed != label)
            ChromeControls.Tooltip(label);
    }
}
