using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public readonly record struct HintRow(string Label, string Value, bool Keycap = false, string? Alt = null);

public static class HintCard {
    private const float MetaGap = 2, Section = 10, KeyGap = 4, LabelGap = 18, RowGap = 2, PairGap = 12;

    public static Vector2 Measure(string? title, string? meta, HintRow[]? rows, float wrap, float s) {
        var size = Vector2.Zero;
        if (!string.IsNullOrEmpty(title))
            using (MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Title)?.Push())
                size = TextDrawing.Measure(title, wrap);
        if (!string.IsNullOrEmpty(meta)) {
            var line = TextDrawing.Measure(meta, wrap);
            size = new(MathF.Max(size.X, line.X), size.Y + (size.Y > 0 ? MetaGap * s : 0) + line.Y);
        }
        if (rows is { Length: > 0 }) {
            var block = RowsSize(rows, s);
            size = new(MathF.Max(size.X, block.X), size.Y + (size.Y > 0 ? Section * s : 0) + block.Y);
        }
        return size;
    }

    public static Vector2 RowsSize(HintRow[] rows, float s) {
        var width = 0f;
        foreach (var row in rows)
            width = MathF.Max(width, TextDrawing.Measure(row.Label).X + LabelGap * s + ValueWidth(row, s));
        return new(width, rows.Length * RowHeight(s) + (rows.Length - 1) * RowGap * s);
    }

    public static void Draw(string? title, string? meta, HintRow[]? rows, float wrap, float s) {
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var size = Measure(title, meta, rows, wrap, s);
        var y = origin.Y;
        if (!string.IsNullOrEmpty(title))
            using (MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Title)?.Push()) {
                TextDrawing.Draw(list, new(origin.X, y), ImGui.GetColorU32(GratisTheme.Primary), title, wrap);
                y += TextDrawing.Measure(title, wrap).Y;
            }
        if (!string.IsNullOrEmpty(meta)) {
            y += y > origin.Y ? MetaGap * s : 0;
            TextDrawing.Draw(list, new(origin.X, y), ImGui.GetColorU32(GratisTheme.Secondary), meta, wrap);
            y += TextDrawing.Measure(meta, wrap).Y;
        }
        if (rows is { Length: > 0 }) {
            if (y > origin.Y) {
                var line = y + Section * s / 2;
                list.AddLine(new(origin.X, line), new(origin.X + size.X, line),
                    ImGui.GetColorU32(GratisTheme.Secondary with { W = .18f }), s);
                y += Section * s;
            }
            DrawRows(list, origin.X, y, size.X, rows, s);
        }
        ImGui.Dummy(size);
    }

    public static void DrawRows(ImDrawListPtr list, float x, float y, float width, HintRow[] rows, float s,
        float alpha = 1) {
        var height = RowHeight(s);
        foreach (var row in rows) {
            var label = TextDrawing.Measure(row.Label);
            TextDrawing.Draw(list, new(x, y + (height - label.Y) / 2),
                ImGui.GetColorU32(GratisTheme.Secondary with { W = alpha }), row.Label);
            var right = x + width;
            if (row.Keycap) {
                right -= ValueWidth(row, s);
                Pill.Draw(list, new(right, y), row.Value, PillFaces.Tag, s, alpha: alpha);
                if (row.Alt != null)
                    Pill.Draw(list, new(right + Pill.Measure(row.Value, PillFaces.Tag, s).X + KeyGap * s, y), row.Alt,
                        PillFaces.Tag, s, alpha: alpha);
            }
            else {
                var value = TextDrawing.Measure(row.Value);
                TextDrawing.Draw(list, new(right - value.X, y + (height - value.Y) / 2),
                    ImGui.GetColorU32(GratisTheme.Primary with { W = alpha }), row.Value);
            }
            y += height + RowGap * s;
        }
    }

    public static float InlineWidth(HintRow[] pairs, float s) {
        var width = 0f;
        foreach (var pair in pairs)
            width += Pill.Measure(pair.Value, PillFaces.Tag, s).X + KeyGap * s + TextDrawing.Measure(pair.Label).X
                + PairGap * s;
        return width - PairGap * s;
    }

    public static void DrawInline(ImDrawListPtr list, float right, float y, HintRow[] pairs, float s) {
        var height = RowHeight(s);
        var x = right - InlineWidth(pairs, s);
        foreach (var pair in pairs) {
            var key = Pill.Measure(pair.Value, PillFaces.Tag, s);
            Pill.Draw(list, new(x, y), pair.Value, PillFaces.Tag, s);
            x += key.X + KeyGap * s;
            var label = TextDrawing.Measure(pair.Label);
            TextDrawing.Draw(list, new(x, y + (height - label.Y) / 2), ImGui.GetColorU32(GratisTheme.Secondary),
                pair.Label);
            x += label.X + PairGap * s;
        }
    }

    public static float RowHeight(float s) => Pill.Measure("A", PillFaces.Tag, s).Y;

    private static float ValueWidth(in HintRow row, float s) => row.Keycap
        ? Pill.Measure(row.Value, PillFaces.Tag, s).X
            + (row.Alt == null ? 0 : KeyGap * s + Pill.Measure(row.Alt, PillFaces.Tag, s).X)
        : TextDrawing.Measure(row.Value).X;
}
