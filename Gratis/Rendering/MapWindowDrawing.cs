using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif
using Gratis.Classes;

namespace Gratis.Rendering;

public static class MapWindowDrawing
{
    private static (int Frame, uint Window, float Top) coordinateLayout;
    public static void DrawSurface(ImDrawListPtr list, Vector2 min, Vector2 max, float radius, float alpha = 1, float? surfaceOpacity = null, GlassSurface surface = GlassSurface.Other)
    {
        var context = MapDrawContext.Current;
        var config = context.Config;
        context.Blur.BeginFrame(ImGui.GetFrameCount());
        var glass = config.IsGratis && config.GlassEnabled;
        var effectiveAlpha = alpha * ImGui.GetStyle().Alpha;
        if (!float.IsFinite(effectiveAlpha) || effectiveAlpha < 1 / 255f) {
            context.Blur.Refuse(BlurRefusal.Alpha, surface);
            return;
        }
        var blurred = TrySurfaceBlur(list, min, max, radius,
            Math.Clamp(float.IsFinite(config.GlassBlurRadius) ? config.GlassBlurRadius : 12, 0, 48) * context.GlobalScale,
            effectiveAlpha, glass && config.GlassBlur, surface: surface);
        var opacity = surfaceOpacity ?? GratisTheme.Surface.W;
        if (glass) {
            var minimum = surfaceOpacity >= 1 ? FrameMaterialDrawing.GlassBackingOpacity(config) : FrameMaterialDrawing.GlassChromeOpacity(config);
            opacity = blurred ? minimum : MathF.Max(.84f, minimum);
        }
        list.AddRectFilled(min, max, ImGui.GetColorU32(GratisTheme.Surface with { W = opacity * alpha }), radius);
        if (glass) {
            FrameMaterialDrawing.DrawGlass(list, min, max, radius, alpha);
            return;
        }
        var stroke = MapDrawContext.Current.GlobalScale;
        list.AddRect(min + new Vector2(.5f * stroke), max - new Vector2(.5f * stroke), ImGui.GetColorU32(GratisTheme.Border with { W = GratisTheme.Border.W * alpha }), radius, ImDrawFlags.None, stroke);
    }

    public static bool DrawWindowBackgroundBlur(ImDrawListPtr list, Vector2 min, Vector2 max, float rounding, bool enabled, float strength) =>
        TrySurfaceBlur(list, min, max, rounding, Math.Clamp(float.IsFinite(strength) ? strength : 3, .05f, 8) * 4,
            ImGui.GetStyle().Alpha, enabled, prepend: true, surface: GlassSurface.Window);

    private static bool TrySurfaceBlur(ImDrawListPtr list, Vector2 min, Vector2 max, float rounding, float radius, float alpha, bool enabled, bool prepend = false, GlassSurface surface = GlassSurface.Other)
    {
        var context = MapDrawContext.Current;
        var diagnostics = context.Blur;
        diagnostics.BeginFrame(ImGui.GetFrameCount());
        if (!enabled || radius <= 0) return diagnostics.Refuse(BlurRefusal.Config, surface);
        if (!float.IsFinite(alpha) || alpha < 1 / 255f) return diagnostics.Refuse(BlurRefusal.Alpha, surface);
        var clippedMin = Vector2.Max(min, list.GetClipRectMin());
        var clippedMax = Vector2.Min(max, list.GetClipRectMax());
        if (clippedMin.X >= clippedMax.X || clippedMin.Y >= clippedMax.Y) return diagnostics.Refuse(BlurRefusal.Clip, surface);
        var size = max - min;
        if (MathF.Abs(size.X - size.Y) < .01f && rounding >= MathF.Min(size.X, size.Y) / 2 &&
            (clippedMin != min || clippedMax != max)) return diagnostics.Refuse(BlurRefusal.Circular, surface);
        rounding = Math.Clamp(rounding, 0, MathF.Min(clippedMax.X - clippedMin.X, clippedMax.Y - clippedMin.Y) / 2);
        if (!context.TryBlurBehind(list, clippedMin, clippedMax, rounding, radius, alpha, prepend)) return diagnostics.Refuse(BlurRefusal.Host, surface);
        diagnostics.Recorded(surface);
        return true;
    }

    private static Vector4 ChromeSecondary => MapDrawContext.Current.Config.GlassEnabled
        ? new Vector4(.78f, .78f, .78f, 1) : GratisTheme.Secondary;

    public static Vector2 ToolbarOffset(int count)
    {
        var context = MapDrawContext.Current;
        var scale = context.GlobalScale;
        var offset = new Vector2(8 * scale);
        if (!context.Config.MapCircle) return offset;
        var width = (count * 28 + 8) * scale;
        var (min, max) = MapShape.Bounds(Vector2.Zero, ImGui.GetWindowSize());
        offset.X = min.X + (max.X - min.X - width) / 2;
        var radius = MapShape.Current;
        var halfWidth = width / 2 + 8 * scale;
        offset.Y += min.Y + radius - MathF.Sqrt(MathF.Max(0, radius * radius - halfWidth * halfWidth));
        return offset;
    }

    private static uint WithAlpha(Vector4 color, float alphaScale)
    {
        color.W *= alphaScale;
        return ImGui.ColorConvertFloat4ToU32(color);
    }

    public static void DrawFrameEffects()
    {
        var config = MapDrawContext.Current.Config;
        var scale = MapDrawContext.Current.GlobalScale;
        var (min, max) = MapShape.Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        var rounding = MapShape.Current;
        var drawList = ImGui.GetWindowDrawList();

        if (config.IsGratis && (config.NeutralFrame || config.FramePreset != FramePreset.Hairline)) {
            FramePresetDrawing.Draw(drawList, min, max, rounding, config, scale);
            return;
        }

        if (config.MapVignette > 0.0f) {
            var depth = MathF.Min(ImGui.GetWindowSize().X, ImGui.GetWindowSize().Y) * 0.2f;
            DrawGradientRing(drawList, min, max, rounding, depth, new Vector4(0.0f, 0.0f, 0.0f, config.MapVignette * 0.75f), 2.0f);
        }

        if (config.MapGlow && !config.IsGratis) {
            var pulse = 1.0f + MathF.Sin((float)ImGui.GetTime() * config.MapGlowSpeed * 2.0f) * config.MapGlowPulse * 0.5f;
            var color = config.MapGlowColor;
            color.W = Math.Clamp(color.W * pulse, 0.0f, 1.0f);
            DrawGradientRing(drawList, min, max, rounding, config.MapGlowSize * scale, color, 2.0f);
        }

        if (config.MapBorder) {
            if (config.IsGratis) FrameMaterialDrawing.DrawStroke(drawList, min, max, rounding,
                config.MapBorderThickness * scale, config.MapBorderColor);
            else {
                var half = config.MapBorderThickness * scale * 0.5f;
                drawList.AddRect(min + new Vector2(half), max - new Vector2(half), ImGui.ColorConvertFloat4ToU32(config.MapBorderColor), MathF.Max(0.0f, rounding - half), ImDrawFlags.None, config.MapBorderThickness * scale);
            }
        }
        if (config.IsGratis) DrawConicRing(drawList, min, max, rounding);
    }

    private static void DrawConicRing(ImDrawListPtr list, Vector2 min, Vector2 max, float radius)
    {
        var context = MapDrawContext.Current;
        if (context.Config.RingEnabled) FrameMaterialDrawing.DrawStroke(list, min, max, radius,
            context.Config.RingThickness * context.GlobalScale, Vector4.One, context.Config);
    }

    private static readonly global::System.Collections.Generic.List<Vector2> ringOuter = [];
    private static readonly global::System.Collections.Generic.List<Vector2> ringInner = [];

    private static void RoundedRectPoints(Vector2 min, Vector2 max, float radius, global::System.Collections.Generic.List<Vector2> points)
    {
        points.Clear();
        radius = MathF.Max(0.0f, MathF.Min(radius, MathF.Min(max.X - min.X, max.Y - min.Y) * 0.5f));
        var segments = MapDrawContext.Current.Config.IsGratis
            ? FrameMaterialDrawing.ArcSegments(MapShape.Current, MathF.PI / 2) : 10;
        var centers = new[] { new Vector2(min.X + radius, min.Y + radius), new Vector2(max.X - radius, min.Y + radius), new Vector2(max.X - radius, max.Y - radius), new Vector2(min.X + radius, max.Y - radius) };
        for (var corner = 0; corner < 4; corner++) {
            var startAngle = MathF.PI * (1.0f + corner * 0.5f);
            for (var step = 0; step <= segments; step++) {
                var angle = startAngle + MathF.PI * 0.5f * step / segments;
                points.Add(centers[corner] + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
            }
        }
    }

    internal static void DrawGradientRing(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float depth, Vector4 color, float power)
    {
        const int bands = 8;
        var white = ImGui.GetFontTexUvWhitePixel();
        for (var band = 0; band < bands; band++) {
            var t0 = band / (float)bands;
            var t1 = (band + 1) / (float)bands;
            RoundedRectPoints(min + new Vector2(depth * t0), max - new Vector2(depth * t0), rounding - depth * t0, ringOuter);
            RoundedRectPoints(min + new Vector2(depth * t1), max - new Vector2(depth * t1), rounding - depth * t1, ringInner);
            var count = ringOuter.Count;
            var outerColor = WithAlpha(color, MathF.Pow(1.0f - t0, power));
            var innerColor = WithAlpha(color, MathF.Pow(1.0f - t1, power));

            drawList.PrimReserve(count * 6, count * 2);
            var baseIndex = (ushort)DrawBinding.VertexIndex(drawList);
            for (var index = 0; index < count; index++) drawList.PrimWriteVtx(ringOuter[index], white, outerColor);
            for (var index = 0; index < count; index++) drawList.PrimWriteVtx(ringInner[index], white, innerColor);
            for (var index = 0; index < count; index++) {
                var next = (index + 1) % count;
                drawList.PrimWriteIdx((ushort)(baseIndex + index));
                drawList.PrimWriteIdx((ushort)(baseIndex + next));
                drawList.PrimWriteIdx((ushort)(baseIndex + count + next));
                drawList.PrimWriteIdx((ushort)(baseIndex + index));
                drawList.PrimWriteIdx((ushort)(baseIndex + count + next));
                drawList.PrimWriteIdx((ushort)(baseIndex + count + index));
            }
        }
    }

    public static void DrawTitleOnMap(string title)
    {
        var config = MapDrawContext.Current.Config;
        if (!config.ShowTitleOnMap || (!config.IsGratis && !config.HideWindowFrame)) return;

        using var chromeAlpha = new ChromeVisibility.AlphaScope(config.IsGratis, MapDrawContext.Current.Chrome.TitleAlpha(config));
        if (!chromeAlpha.Visible) return;
        uint TitleColor(Vector4 color) => config.IsGratis ? ImGui.GetColorU32(color) : ImGui.ColorConvertFloat4ToU32(color);
        var idIndex = title.IndexOf("###");
        if (idIndex >= 0) title = title[..idIndex];
        if (string.IsNullOrWhiteSpace(title)) return;
        if (config.IsGratis && config.CompactTitle) {
            DrawCompactTitle(title);
            return;
        }

        var parts = title.Split(" - ", global::System.StringSplitOptions.RemoveEmptyEntries | global::System.StringSplitOptions.TrimEntries);
        var lines = new global::System.Collections.Generic.List<(string Text, float Scale, Vector4 Color)>();
        if (config.TitleStacked && parts.Length > 1) {
            var headline = (parts[^1], config.TitleScale, config.TitleColor);
            var secondary = (string.Join(config.TitleSeparator, parts[..^1]), config.TitleScale * config.TitleSecondaryScale, config.TitleSecondaryColor);
            if (config.TitleSecondaryAbove) {
                lines.Add(secondary);
                lines.Add(headline);
            }
            else {
                lines.Add(headline);
                lines.Add(secondary);
            }
        }
        else {
            lines.Add((string.Join(config.TitleSeparator, parts), config.TitleScale, config.TitleColor));
        }

        var scale = MapDrawContext.Current.GlobalScale;
        var baseFontSize = ImGui.GetFontSize();
        var spacing = config.TitleLineSpacing * scale;
        var sizes = new Vector2[lines.Count];
        var textSize = Vector2.Zero;
        var handles = new IDrawFont?[lines.Count];
        for (var index = 0; index < lines.Count; index++) {
            handles[index] = MapDrawContext.Current.GetTitleFont(config.TitleFont, baseFontSize * lines[index].Scale);
            using (handles[index]?.Push()) {
                sizes[index] = ImGui.CalcTextSize(lines[index].Text) * (handles[index] is null ? lines[index].Scale : 1.0f);
            }

            textSize.X = sizes[index].X > textSize.X ? sizes[index].X : textSize.X;
            textSize.Y += sizes[index].Y + (index > 0 ? spacing : 0.0f);
        }

        var padding = config.TitleBackground ? config.TitlePadding * scale : Vector2.Zero;
        var boxSize = textSize + padding * 2.0f;
        var offset = config.TitleOffset * scale;

        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        var isRight = config.TitleCorner is 1 or 3;
        var isBottom = config.TitleCorner is 2 or 3;
        var x = isRight ? windowSize.X - boxSize.X - offset.X : offset.X;
        var y = isBottom ? windowSize.Y - boxSize.Y - offset.Y : offset.Y;
        if (!isBottom && config.TitleBelowToolbar && isRight) y += ImGui.GetFrameHeight();

        var boxMin = windowPos + new Vector2(x, y);
        var boxMax = boxMin + boxSize;
        var drawList = ImGui.GetWindowDrawList();

        if (config.TitleBackground) {
            var rounding = config.TitleRounding * scale;
            drawList.AddRectFilled(boxMin, boxMax, TitleColor(config.TitleBackgroundColor), rounding);
            if (config.TitleBorder) {
                drawList.AddRect(boxMin, boxMax, TitleColor(config.TitleBorderColor), rounding, ImDrawFlags.None, 1.0f);
            }
        }

        var font = ImGui.GetFont();
        var outline = TitleColor(config.TitleOutlineColor);
        var cursorY = boxMin.Y + padding.Y;
        for (var index = 0; index < lines.Count; index++) {
            var line = lines[index];
            var lineX = isRight ? boxMax.X - padding.X - sizes[index].X : boxMin.X + padding.X;
            var linePosition = new Vector2(lineX, cursorY);
            using (handles[index]?.Push()) {
                var lineFont = handles[index] is null ? font : ImGui.GetFont();
                var fontSize = handles[index] is null ? baseFontSize * line.Scale : ImGui.GetFontSize();
                var color = TitleColor(line.Color);
                var bold = new Vector2(config.TitleBold, 0.0f);
                if (config.TitleOutline) {
                    foreach (var step in new[] { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1), new Vector2(0, 1) }) {
                        drawList.AddText(lineFont, fontSize, linePosition + step, outline, line.Text);
                        if (config.TitleBold > 0.0f) drawList.AddText(lineFont, fontSize, linePosition + step + bold, outline, line.Text);
                    }
                }

                drawList.AddText(lineFont, fontSize, linePosition, color, line.Text);
                if (config.TitleBold > 0.0f) drawList.AddText(lineFont, fontSize, linePosition + bold, color, line.Text);
            }

            cursorY += sizes[index].Y + spacing;
        }
    }

    private static void DrawCompactTitle(string title)
    {
        var context = MapDrawContext.Current;
        var scale = context.GlobalScale;
        var parts = title.Split(" - ", global::System.StringSplitOptions.RemoveEmptyEntries | global::System.StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return;
        var name = parts[^1];
        var region = parts.Length > 1 ? string.Join(" / ", parts[..^1]) : "";
        using var titleFont = context.GetChromeFont(14 * scale, ChromeFontRole.Chip)?.Push();
        var font = ImGui.GetFont();
        var window = ImGui.GetWindowSize();
        var (areaMin, areaMax) = MapShape.Bounds(Vector2.Zero, window);
        var area = areaMax - areaMin;
        if (area.X < 160 * scale || area.Y < 160 * scale) return;
        var compact = area.X < 480 * scale || area.Y < 360 * scale;
        if (compact) region = "";
        var padding = new Vector2(compact ? 8 : 12, compact ? 6 : 8) * scale;
        var wrap = MathF.Max(1, MathF.Min(280 * scale,
            (context.Config.MapCircle ? area.X * .72f : area.X - 32 * scale) - padding.X * 2));
        var primary = DrawBinding.MeasureText(14 * scale, name, wrap);
        Vector2 secondary;
        using (context.GetChromeFont(12 * scale, ChromeFontRole.Body)?.Push())
            secondary = DrawBinding.MeasureText(12 * scale, region, wrap);
        var size = new Vector2(MathF.Max(primary.X, secondary.X), primary.Y + (region.Length > 0 ? secondary.Y + 4 * scale : 0)) + padding * 2;
        if (size.Y > area.Y * .35f) return;
        var config = MapDrawContext.Current.Config;
        var count = config.UseCustomWindowChrome ? 8 : 7;
        var y = 8 * scale;
        if (config.MapCircle) {
            var half = area.X / 2;
            var rimInset = half - MathF.Sqrt(MathF.Max(0, half * half - size.X * size.X / 4));
            var bottom = coordinateLayout.Frame == ImGui.GetFrameCount() && coordinateLayout.Window == ImGui.GetID("coordinate-layout")
                ? coordinateLayout.Top - ImGui.GetWindowPos().Y : areaMax.Y - rimInset;
            y = MathF.Max(8 * scale, bottom - size.Y - 8 * scale);
        }
        if (!config.MapCircle && size.X + (count * 28 + 32) * scale > window.X) y = 52 * scale;
        var inset = 8 * scale;
        var radius = MapShape.Current;
        if (y < radius) inset += radius - MathF.Sqrt(MathF.Max(0, radius * radius - (radius - y) * (radius - y)));
        var x = config.MapCircle ? (window.X - size.X) / 2 : MathF.Max(8 * scale, window.X - inset - size.X);
        var pos = ImGui.GetWindowPos() + new Vector2(x, y);
        var list = ImGui.GetWindowDrawList();
        DrawSurface(list, pos, pos + size, GratisTheme.CardRadius * scale, surface: GlassSurface.Title);
        var text = pos + padding;
        if (region.Length > 0) {
            using var secondaryFont = context.GetChromeFont(12 * scale, ChromeFontRole.Body)?.Push();
            list.AddText(ImGui.GetFont(), 12 * scale, text, ImGui.GetColorU32(ChromeSecondary), region, wrap);
            text.Y += secondary.Y + 4 * scale;
        }
        list.AddText(font, 14 * scale, text, ImGui.GetColorU32(GratisTheme.Primary), name, wrap);
    }

    public static void DrawCoordinates(Vector2? character, Vector2? cursor)
    {
        if (character is null && cursor is null) return;
        var context = MapDrawContext.Current;
        var (areaMin, areaMax) = MapShape.Bounds(Vector2.Zero, ImGui.GetWindowSize());
        var area = areaMax - areaMin;
        if (context.Config.IsGratis && (area.X < 360 * context.GlobalScale || area.Y < 300 * context.GlobalScale)) return;
        using var chromeAlpha = new ChromeVisibility.AlphaScope(context.Config.IsGratis, context.Chrome.ControlsAlpha(context.Config));
        if (!chromeAlpha.Visible) return;
        var scale = MapDrawContext.Current.GlobalScale;
        using var coordinateFont = MapDrawContext.Current.GetChromeFont(14 * scale, ChromeFontRole.Coordinates)?.Push();
        var font = ImGui.GetFont();
        var fontSize = 14 * scale;
        var digit = DrawBinding.MeasureText(fontSize, "0").X;
        string Format(Vector2 point) => global::System.FormattableString.Invariant($"{point.X,4:F1} / {point.Y,4:F1}");
        var left = character is { } player ? "PLAYER " + Format(player) : "";
        var right = cursor is { } mouse ? "CURSOR " + Format(mouse) : "";
        var text = left + (left.Length > 0 && right.Length > 0 ? "  |  " : "") + right;
        float Advance(char glyph) => char.IsDigit(glyph) || glyph == '.' ? digit :
            glyph == ' ' ? 3 * scale : DrawBinding.MeasureText(12 * scale, glyph.ToString()).X;
        float TextWidth(string value) { var width = 0f; foreach (var glyph in value) width += Advance(glyph); return width; }
        var size = new Vector2(TextWidth(text) + 24 * scale, 36 * scale);
        var window = ImGui.GetWindowSize();
        if (size.X > window.X - 32 * scale && left.Length > 0) {
            text = left; size.X = TextWidth(text) + 24 * scale;
        }
        if (size.X > area.X - 32 * scale) return;
        var pos = ImGui.GetWindowPos() + new Vector2((window.X - size.X) / 2, areaMax.Y - size.Y - 8 * scale);
        if (MapDrawContext.Current.Config.MapCircle) {
            var half = area.X * .5f;
            pos.Y -= half - MathF.Sqrt(MathF.Max(0, half * half - size.X * size.X * .25f));
        }
        coordinateLayout = (ImGui.GetFrameCount(), ImGui.GetID("coordinate-layout"), pos.Y);
        ImGui.SetCursorScreenPos(pos);
        ImGui.Dummy(size);
        var list = ImGui.GetWindowDrawList();
        DrawSurface(list, pos, pos + size, GratisTheme.ButtonRadius * scale, surface: GlassSurface.Coordinates);
        var start = pos + new Vector2(12, 8) * scale;
        for (var index = 0; index < text.Length; index++) {
            var glyph = text[index].ToString();
            var numeric = char.IsDigit(text[index]) || text[index] == '.';
            var glyphSize = numeric ? fontSize : 12 * scale;
            var width = DrawBinding.MeasureText(glyphSize, glyph).X;
            var advance = Advance(text[index]);
            list.AddText(font, glyphSize, start + new Vector2((advance - width) / 2, (fontSize - glyphSize) / 2), ImGui.GetColorU32(numeric ? GratisTheme.Primary : ChromeSecondary), glyph);
            start.X += advance;
        }
    }
}
