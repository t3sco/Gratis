using System.Collections.Generic;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif
using Math = global::System.Math;

namespace Gratis.Rendering;

public static class HoverCardDrawing {
    private const int MaxRows = 4;
    private const uint MotionId = 0xfac31102;
    private readonly record struct Entry(string Key, string Title, string? Secondary, MarkerCategory? Category,
        Classes.MarkerInfo? Marker);

    private static readonly List<Entry> queued = [], shown = [];
    private static readonly (Entry Entry, float Swatch, Vector2 Primary, Vector2 Secondary)[] layout =
        new (Entry, float, Vector2, Vector2)[MaxRows];
    private static MapDrawContext? owner;
    private static int queuedFrame = -1;
    private static Vector2 mouse;
    public static int Rows { get; private set; }

    private static int Rank(MarkerCategory? category) => category switch {
        MarkerCategory.Objective => 0,
        MarkerCategory.Transport => 1,
        MarkerCategory.Link => 2,
        MarkerCategory.Service => 3,
        MarkerCategory.Poi or MarkerCategory.Auto or null => 4,
        _ => 5,
    };

    public static void Draw(string key, string title, string? secondary = null, MarkerCategory? category = null,
        Classes.MarkerInfo? marker = null) {
        var context = MapDrawContext.Current;
        context.HoverCardFrame = ImGui.GetFrameCount();
        var attuned = AetheryteAttunement.Status(context.Config, marker);
        if (attuned.HasValue)
            secondary = (attuned.Value ? "Attuned" : "Not yet attuned")
                + (string.IsNullOrWhiteSpace(secondary) ? "" : "\n" + secondary);
        if (marker != null && context.Assistance?.HoverTransport(marker, title) == true)
            return;
        if (queuedFrame != ImGui.GetFrameCount() || !ReferenceEquals(owner, context)) {
            queued.Clear();
            queuedFrame = ImGui.GetFrameCount();
            owner = context;
        }
        foreach (var other in queued)
            if (marker != null ? ReferenceEquals(other.Marker, marker) : other.Key == key)
                return;
        queued.Add(new(key, title, secondary, category, marker));
    }

    public static void DrawTail() {
#if GRATIS_PREVIEW
        using var benchmark = BenchmarkProbe.Measure(BenchmarkSection.HoverCard);
#endif
        if (!ReferenceEquals(owner, MapDrawContext.Current))
            return;
        if (queuedFrame == ImGui.GetFrameCount() && queued.Count > 0) {
            shown.Clear();
            shown.AddRange(queued);
            shown.Sort((a, b) => Rank(a.Category).CompareTo(Rank(b.Category)));
            mouse = ImGui.GetMousePos();
            Render(false);
            return;
        }
        if (shown.Count == 0)
            return;
        if (UiMotion.Visibility(MotionId, false) <= .001f) {
            shown.Clear();
            return;
        }
        Render(true);
    }

    private static void Render(bool tail) {
        var context = MapDrawContext.Current;
        context.Chrome.Reveal(ImGui.GetTime());
        var scale = GratisTheme.ChromeScale;
        var fade = UiMotion.Visibility(MotionId, !tail);
        var viewport = ImGui.GetWindowViewport();
        var margin = new Vector2(GratisTheme.Inset * scale);
        var available = Vector2.Max(new Vector2(1), viewport.Size - margin * 2);
        var padding = new Vector2(12 * scale);
        var titleSize = 14 * scale;
        var secondarySize = 12 * scale;
        var rowGap = 10 * scale;
        var rows = Math.Min(shown.Count, MaxRows);
        Rows = shown.Count;
        var hidden = shown.Count - rows;
        var width = 0f;
        var height = 0f;
        for (var i = 0; i < rows; i++) {
            var entry = shown[i];
            var attuned = AetheryteAttunement.Status(context.Config, entry.Marker);
            var swatch = (context.Config.HoverCardSwatch && entry.Category.HasValue ? 24 * scale : 0)
                + (attuned.HasValue ? 18 * scale : 0);
            var wrap = Math.Max(1, Math.Min(300 * scale, available.X - padding.X * 2 - swatch));
            Vector2 primary, secondary;
            using (context.GetChromeFont(titleSize, ChromeFontRole.Title)?.Push())
                primary = DrawBinding.MeasureText(titleSize, entry.Title, wrap);
            using (context.GetChromeFont(secondarySize, ChromeFontRole.Body)?.Push())
                secondary = string.IsNullOrEmpty(entry.Secondary) ? Vector2.Zero
                    : DrawBinding.MeasureText(secondarySize, entry.Secondary, wrap);
            layout[i] = (entry, swatch, primary, secondary);
            width = Math.Max(width, Math.Max(primary.X, secondary.X) + swatch);
            height += primary.Y + (secondary.Y > 0 ? 4 * scale + secondary.Y : 0) + (i > 0 ? rowGap : 0);
        }
        var more = hidden > 0 ? $"+{hidden} more here" : "";
        Vector2 moreSize = Vector2.Zero;
        if (more.Length > 0)
            using (context.GetChromeFont(secondarySize, ChromeFontRole.Body)?.Push()) {
                moreSize = DrawBinding.MeasureText(secondarySize, more);
                width = Math.Max(width, moreSize.X);
                height += rowGap + moreSize.Y;
            }
        var size = Vector2.Min(new Vector2(width, height) + padding * 2, available);
        var position = mouse + new Vector2(24, 24 + (UiMotion.Full ? 4 * (1 - fade) : 0)) * scale;
        if (position.Y + size.Y > viewport.Pos.Y + viewport.Size.Y - margin.Y)
            position.Y = mouse.Y - size.Y - 24 * scale;
        position = Vector2.Clamp(position, viewport.Pos + margin, viewport.Pos + viewport.Size - margin - size);
        var list = ImGui.GetForegroundDrawList();
        using var titleFont = context.GetChromeFont(titleSize, ChromeFontRole.Title)?.Push();
        DrawBinding.PushFontTexture(list);
        MapWindowDrawing.DrawSurface(list, position, position + size, GratisTheme.Radius.Card * scale, fade,
            surfaceOpacity: 1);
        list.PushClipRect(position, position + size, true);
        var y = position.Y + padding.Y;
        for (var i = 0; i < rows; i++) {
            var (entry, swatch, primary, secondary) = layout[i];
            if (i > 0) {
                var line = y - rowGap / 2;
                list.AddLine(new(position.X + padding.X, line), new(position.X + size.X - padding.X, line),
                    ImGui.GetColorU32(GratisTheme.Secondary with { W = .18f * fade }), scale);
            }
            DrawRow(list, entry, new(position.X + padding.X, y), swatch, primary.Y, secondary.Y > 0, fade, scale,
                titleSize, secondarySize, Math.Max(1, Math.Min(300 * scale, available.X - padding.X * 2 - swatch)));
            y += primary.Y + (secondary.Y > 0 ? 4 * scale + secondary.Y : 0) + rowGap;
        }
        if (more.Length > 0)
            using (context.GetChromeFont(secondarySize, ChromeFontRole.Body)?.Push()) {
                DrawBinding.PushFontTexture(list);
                list.AddText(ImGui.GetFont(), secondarySize, new(position.X + padding.X, y),
                    ImGui.GetColorU32(GratisTheme.Secondary with { W = fade }), more);
                DrawBinding.PopTexture(list);
            }
        list.PopClipRect();
        DrawBinding.PopTexture(list);
    }

    private static void DrawRow(ImDrawListPtr list, Entry entry, Vector2 origin, float swatch, float titleHeight,
        bool hasSecondary, float fade, float scale, float titleSize, float secondarySize, float wrap) {
        var context = MapDrawContext.Current;
        var marker = entry.Marker;
        var attuned = AetheryteAttunement.Status(context.Config, marker);
        var text = origin + new Vector2(swatch, 0);
        if (attuned.HasValue)
            GratisIcons.Draw(list, text + new Vector2(-10, 7) * scale,
                attuned.Value ? GratisIcons.Unlock : GratisIcons.Lock, GratisTheme.Secondary with { W = fade },
                12 * scale);
        if (context.Config.HoverCardSwatch && swatch > 0 && entry.Category is { } shape) {
            var center = origin + new Vector2(7, 7) * scale;
            if (marker != null && MarkerVisual.UseFlatMarker(marker))
                FlatMarkerDrawing.Symbol(list, center, 7 * scale, marker, fade);
            else if (marker != null) {
                var icon = context.GetIcon(marker.IconId);
                list.AddImage(icon.Handle, center - new Vector2(8 * scale), center + new Vector2(8 * scale),
                    Vector2.Zero, Vector2.One,
                    ImGui.GetColorU32(Vector4.One with { W = fade }));
            }
            else
                FlatMarkerDrawing.Shape(list, center, 6 * scale, shape, FlatMarkerDrawing.Color(shape), fade);
        }
        list.AddText(ImGui.GetFont(), titleSize, text, ImGui.GetColorU32(GratisTheme.Primary with { W = fade }),
            entry.Title, wrap);
        if (!hasSecondary || string.IsNullOrEmpty(entry.Secondary))
            return;
        using var secondaryFont = context.GetChromeFont(secondarySize, ChromeFontRole.Body)?.Push();
        DrawBinding.PushFontTexture(list);
        list.AddText(ImGui.GetFont(), secondarySize, text + new Vector2(0, titleHeight + 4 * scale),
            ImGui.GetColorU32(GratisTheme.Secondary with { W = fade }), entry.Secondary, wrap);
        DrawBinding.PopTexture(list);
    }
}
