using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public enum ActionFeedback { None, Fill, Outline, Selected }
public enum ActionLabel { Left, Center, Fit, Native }
public enum ActionDisabled { Native, Dim }

public readonly record struct ActionProfile {
    public float Height { get; init; }
    public float HeightInset { get; init; }
    public float WidthPadding { get; init; }
    public float Radius { get; init; }
    public float RadiusInset { get; init; }
    public float TextInset { get; init; }
    public float IconInset { get; init; }
    public float IconSize { get; init; }
    public float TrailingInset { get; init; }
    public float TrailingGap { get; init; }
    public Vector4 Fill { get; init; }
    public Vector4 HoverFill { get; init; }
    public Vector4 Border { get; init; }
    public Vector4 Ink { get; init; }
    public Vector4 IconInk { get; init; }
    public Vector4 TrailingInk { get; init; }
    public ActionFeedback Feedback { get; init; }
    public ActionLabel Label { get; init; }
    public ActionDisabled Disabled { get; init; }
    public float DisabledAlpha { get; init; }
    public bool Control { get; init; }
    public bool IconBeforeFeedback { get; init; }
    public bool DisabledScope { get; init; }
    public string? Tooltip { get; init; }
    public bool WindowTooltip { get; init; }
}

public static class ActionFaces {
    public static readonly ActionProfile Quiet = new() {
        Height = GratisTheme.ControlHeight, WidthPadding = 16, Radius = GratisTheme.Radius.Control,
        TextInset = 8, Ink = GratisTheme.Secondary, Feedback = ActionFeedback.Fill, Control = true,
    };
    public static readonly ActionProfile Action = Quiet with {
        WidthPadding = 38, TextInset = 27, IconInset = 13, IconSize = 14,
        Fill = GratisTheme.Crystal with { W = .20f }, HoverFill = GratisTheme.Crystal with { W = .30f },
        Border = GratisTheme.Crystal with { W = .45f },
        Ink = GratisTheme.Primary, IconInk = GratisTheme.Crystal,
        Feedback = ActionFeedback.Outline, IconBeforeFeedback = true,
    };
    public static readonly ActionProfile PrimaryTravel = Quiet with {
        WidthPadding = 40, TextInset = 30, IconInset = 15, IconSize = GratisTheme.IconSize,
        Fill = GratisTheme.Crystal with { W = .94f }, HoverFill = GratisTheme.Crystal,
        Ink = GratisTheme.Surface with { W = 1 }, IconInk = GratisTheme.Surface with { W = 1 },
        Feedback = ActionFeedback.Outline,
    };
    public static readonly ActionProfile Send = PrimaryTravel with { Disabled = ActionDisabled.Dim, DisabledAlpha = .4f };
    public static readonly ActionProfile Travel = new() {
        Height = GratisTheme.ControlHeight, Radius = GratisTheme.Radius.Control, TextInset = 14,
        TrailingInset = 14, TrailingGap = 12, Label = ActionLabel.Fit,
        Fill = GratisTheme.Crystal with { W = .20f }, HoverFill = GratisTheme.Crystal with { W = .30f },
        Border = GratisTheme.Crystal with { W = .45f }, Ink = GratisTheme.Primary,
        TrailingInk = GratisTheme.Crystal, Feedback = ActionFeedback.Outline,
        Control = true, DisabledScope = true,
    };
    public static readonly ActionProfile ConfirmTravel = Travel with {
        Fill = GratisTheme.Crystal with { W = .38f }, HoverFill = GratisTheme.Crystal with { W = .5f },
        Border = GratisTheme.Crystal with { W = .85f },
    };
    public static readonly ActionProfile Update = new() {
        Height = GratisTheme.ControlHeight, HeightInset = 8, WidthPadding = 24,
        Radius = GratisTheme.Radius.Card, RadiusInset = 4, TextInset = 12,
        Fill = GratisTheme.Crystal with { W = .20f }, HoverFill = GratisTheme.Crystal with { W = .30f },
        Border = GratisTheme.Crystal with { W = .45f }, Ink = GratisTheme.Primary,
        Tooltip = "Opens the plugin installer so you can update Gratis", WindowTooltip = true,
    };
    public static readonly ActionProfile QuestMap = new() {
        Height = GratisTheme.ControlHeight, Radius = GratisTheme.Radius.Control,
        Ink = GratisTheme.WarmAccent, Feedback = ActionFeedback.Selected, Label = ActionLabel.Center,
        DisabledScope = true, Tooltip = "Show on map  M / Shift+Enter",
    };
    public static readonly ActionProfile NativeText = new() {
        Radius = GratisTheme.Radius.Control, Label = ActionLabel.Native,
        Feedback = ActionFeedback.Fill, Control = true,
    };
}

public readonly record struct ActionOptions(ActionProfile Profile, char Icon = '\0', string TrailingText = "",
    bool Enabled = true, Vector2 Size = default, string? IdSuffix = null, float? Scale = null,
    float? Radius = null, ActionLabelCache? LabelCache = null);

public sealed class ActionLabelCache {
    private string source = "", fitted = "";
    private float width = -1, fontSize;
    private ImFontPtr font;
    private MapDrawContext? context;

    internal unsafe string Fit(string label, float available) {
        var current = ImGui.GetFont();
#if GRATIS_PREVIEW
        var sameFont = SameFont(font, current);
#else
        var sameFont = font.Handle == current.Handle;
#endif
        if (source != label || width != available || !sameFont || fontSize != ImGui.GetFontSize()
            || !ReferenceEquals(context, MapDrawContext.Current)) {
            source = label;
            width = available;
            font = current;
            fontSize = ImGui.GetFontSize();
            context = MapDrawContext.Current;
            fitted = ChromeControls.FitLabel(label, available);
        }
        return fitted;
    }
#if GRATIS_PREVIEW
    private static unsafe bool SameFont(ImFontPtr a, ImFontPtr b) => a.NativePtr == b.NativePtr;
#endif
}

public static class ActionButton {
    public static bool Draw(string id, string label, in ActionOptions options) {
        var face = options.Profile;
        id = options.IdSuffix ?? id;
        var dim = !options.Enabled && face.Disabled == ActionDisabled.Dim;
        var disabled = face.DisabledScope || !options.Enabled && !dim;
        if (dim) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * face.DisabledAlpha);
        if (disabled) ImGui.BeginDisabled(!options.Enabled);
        try {
            var s = options.Scale ?? GratisTheme.ChromeScale;
            if (face.Label == ActionLabel.Native)
                return DrawNative(id, options.Size, s, options.Radius, face);
            var pos = ImGui.GetCursorScreenPos();
            var size = options.Size != default ? options.Size
                : new Vector2(ImGui.CalcTextSize(label).X + face.WidthPadding * s,
                    face.Height * s - face.HeightInset * s);
            var clicked = ChromeControls.Button(id, size);
            var list = ImGui.GetWindowDrawList();
            using var motion = face.Control ? UiMotion.Control(list, pos, pos + size) : (UiMotion.VisualScope?)null;
            var radius = options.Radius ?? GratisTheme.Radius.Inner(face.Radius * s, face.RadiusInset * s);
            if (face.Fill.W != 0)
                list.AddRectFilled(pos, pos + size,
                    ImGui.GetColorU32(ImGui.IsItemHovered() ? face.HoverFill : face.Fill), radius);
            if (face.Border.W != 0)
                list.AddRect(pos, pos + size, ImGui.GetColorU32(face.Border), radius, ImDrawFlags.None, s);
            if (face.IconBeforeFeedback) DrawIcon(list, pos, size, s, face, options.Icon);
            if (face.Feedback != ActionFeedback.None)
                ChromeControls.Feedback(list, pos, pos + size, face.Feedback == ActionFeedback.Selected,
                    face.Feedback is ActionFeedback.Fill or ActionFeedback.Selected);
            if (!face.IconBeforeFeedback) DrawIcon(list, pos, size, s, face, options.Icon);
            if (face.Label == ActionLabel.Center)
                TextDrawing.Draw(list, pos + (size - TextDrawing.Measure(label)) / 2,
                    ImGui.GetColorU32(face.Ink), label);
            else {
                var trailing = options.TrailingText;
                var costWidth = trailing.Length > 0 ? ImGui.CalcTextSize(trailing).X + face.TrailingGap * s : 0;
                var displayed = face.Label == ActionLabel.Fit
                    ? (options.LabelCache ?? throw new ArgumentException("Travel faces need a fitted-label cache."))
                        .Fit(label, size.X - 2 * face.TextInset * s - costWidth) : label;
                var y = (size.Y - ImGui.GetFontSize()) / 2;
                list.AddText(pos + new Vector2(face.TextInset * s, y), ImGui.GetColorU32(face.Ink), displayed);
                if (trailing.Length > 0)
                    list.AddText(pos + new Vector2(size.X - face.TrailingInset * s - ImGui.CalcTextSize(trailing).X, y),
                        ImGui.GetColorU32(face.TrailingInk), trailing);
            }
            if (face.Tooltip is { } tooltip)
                ChromeControls.Tooltip(tooltip,
                    windowBounds: face.WindowTooltip ? MapToolbarDrawing.WindowBounds : null);
            return clicked;
        }
        finally {
            if (disabled) ImGui.EndDisabled();
            if (dim) ImGui.PopStyleVar();
        }
    }

    private static void DrawIcon(ImDrawListPtr list, Vector2 pos, Vector2 size, float s,
        in ActionProfile face, char icon) {
        if (face.IconSize > 0)
            GratisIcons.Draw(list, pos + new Vector2(face.IconInset * s, size.Y / 2), icon, face.IconInk, face.IconSize * s);
    }

    private static bool DrawNative(string id, Vector2 size, float s, float? radius, in ActionProfile face) {
        if (!UiMotion.IsGratis) return ImGui.Button(id, size);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, radius ?? face.Radius * s);
        var list = ImGui.GetWindowDrawList();
        var first = list.VtxBuffer.Size;
        var min = ImGui.GetCursorScreenPos();
        var fill = ImGui.GetStyle().Colors[(int)ImGuiCol.Button];
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, fill);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, fill);
#if GRATIS_PREVIEW
        ChromeControls.PreviewBeforeButton?.Invoke(id);
#endif
        var clicked = ImGui.Button(id, size);
#if GRATIS_PREVIEW
        ChromeControls.PreviewAfterButton?.Invoke(id);
#endif
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar();
        UiMotion.ItemId(id);
        var max = ImGui.GetItemRectMax();
        using var press = UiMotion.Control(list, min, max, first);
        ChromeControls.Feedback(list, min, max, scale: s, radius: radius);
        return clicked;
    }
}
