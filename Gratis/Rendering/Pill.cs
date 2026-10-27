using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public enum PillLayout { Label, Heading, Metric, BottomRight, Dot }
public enum PillRounding { Capsule, Control, Inner }

public readonly record struct PillProfile {
    public PillLayout Layout { get; init; }
    public Vector2 Padding { get; init; }
    public Vector2 Inset { get; init; }
    public Vector4 Fill { get; init; }
    public Vector4 Ink { get; init; }
    public float Height { get; init; }
    public float Radius { get; init; }
    public float RadiusInset { get; init; }
    public PillRounding Rounding { get; init; }
}

public static class PillFaces {
    public static readonly PillProfile PaletteRow = new() {
        Padding = new(12, 3), Inset = new(6, 1), Fill = GratisTheme.Primary with { W = .055f },
    };
    public static readonly PillProfile HeadingDetail = new() {
        Layout = PillLayout.Heading, Padding = new(8, 4), Inset = new(4, 2),
        Fill = GratisTheme.WarmAccent with { W = .10f }, Ink = GratisTheme.Secondary,
    };
    public static readonly PillProfile PinLabel = new() {
        Padding = new(12, 6), Inset = new(6, 3), Fill = GratisTheme.Outline with { W = .95f },
        Ink = GratisTheme.Primary, Rounding = PillRounding.Control,
    };
    public static readonly PillProfile RewardMetric = new() {
        Layout = PillLayout.Metric, Padding = new(20, 0), Height = 30,
        Fill = new(0, 0, 0, .08f), Rounding = PillRounding.Control,
    };
    public static readonly PillProfile RewardCount = new() {
        Layout = PillLayout.BottomRight, Padding = new(8, 4), Inset = new(4, 2),
        Fill = GratisTheme.Surface with { W = 1 }, Ink = GratisTheme.Primary,
        Rounding = PillRounding.Inner, RadiusInset = 2,
    };
    public static readonly PillProfile SettingsDot = new() {
        Layout = PillLayout.Dot, Inset = new(28, 8), Radius = 2.5f, Fill = GratisTheme.WarmAccent,
    };
    public static readonly PillProfile FilterDot = new() {
        Layout = PillLayout.Dot, Inset = new(28, 8), Radius = 2.5f, Fill = GratisTheme.WarmAccent,
    };
}

public static class Pill {
    public static Vector2 Measure(string label, in PillProfile face, float scale) {
        var text = face.Layout == PillLayout.Heading ? ImGui.CalcTextSize(label) : TextDrawing.Measure(label);
        return face.Layout == PillLayout.Metric
            ? new(text.X + face.Padding.X * scale, face.Height * scale)
            : text + face.Padding * scale;
    }

    public static void Draw(ImDrawListPtr list, Vector2 anchor, string label, in PillProfile face,
        float scale, Vector4? ink = null, float? alpha = null) {
        if (face.Layout == PillLayout.Dot) {
            list.AddCircleFilled(anchor + face.Inset * scale, face.Radius * scale,
                ImGui.GetColorU32(alpha is { } a ? face.Fill with { W = a } : face.Fill));
            return;
        }
        var size = Measure(label, face, scale);
        var min = face.Layout == PillLayout.Heading ? anchor - face.Inset * scale
            : face.Layout == PillLayout.BottomRight ? anchor - size : anchor;
        var max = face.Layout == PillLayout.Heading
            ? anchor + ImGui.CalcTextSize(label) + face.Inset * scale
            : face.Layout == PillLayout.BottomRight ? anchor : min + size;
        var radius = face.Rounding == PillRounding.Control ? GratisTheme.Radius.Control * scale
            : face.Rounding == PillRounding.Inner
                ? GratisTheme.Radius.Inner(GratisTheme.Radius.Control * scale, face.RadiusInset * scale)
                : GratisTheme.Radius.Pill(size);
        var colour = ink ?? face.Ink;
        list.AddRectFilled(min, max, ImGui.GetColorU32(face.Layout == PillLayout.Metric
            ? colour with { W = face.Fill.W } : face.Fill), radius);
        if (face.Layout == PillLayout.Heading) ImGui.TextColored(colour, label);
        else {
            var position = face.Layout == PillLayout.Metric
                ? min + (size - TextDrawing.Measure(label)) / 2 : min + face.Inset * scale;
            TextDrawing.Draw(list, position, ImGui.GetColorU32(colour), label);
            if (face.Layout == PillLayout.Metric) ImGui.Dummy(size);
        }
    }
}
