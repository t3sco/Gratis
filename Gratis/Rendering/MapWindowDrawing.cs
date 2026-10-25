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
    private static int boundsFrame;
    private static MapDrawContext? boundsContext;
    private static Vector4 titleBounds, coordinateBounds;
    public static Vector4 TitleBounds => boundsFrame == ImGui.GetFrameCount() && ReferenceEquals(boundsContext, MapDrawContext.Current) ? titleBounds : Vector4.Zero;
    public static Vector4 CoordinateBounds => boundsFrame == ImGui.GetFrameCount() && ReferenceEquals(boundsContext, MapDrawContext.Current) ? coordinateBounds : Vector4.Zero;
    internal static void RecordBounds(Vector2 min, Vector2 max, bool title)
    {
        if (boundsFrame != ImGui.GetFrameCount() || !ReferenceEquals(boundsContext, MapDrawContext.Current)) {
            titleBounds = coordinateBounds = Vector4.Zero; boundsFrame = ImGui.GetFrameCount(); boundsContext = MapDrawContext.Current;
        }
        if (title) titleBounds = new(min.X, min.Y, max.X, max.Y);
        else coordinateBounds = new(min.X, min.Y, max.X, max.Y);
    }
    public static void DrawSurface(ImDrawListPtr list, Vector2 min, Vector2 max, float radius, float alpha = 1, float? surfaceOpacity = null, GlassSurface surface = GlassSurface.Other)
    {
        var context = MapDrawContext.Current;
        var config = context.Config;
        context.Blur.BeginFrame(ImGui.GetFrameCount());
        if (surface is GlassSurface.Toolbar or GlassSurface.Title or GlassSurface.Coordinates)
            context.Trace?.Chrome(surface.ToString());
        var glass = config.IsGratis && config.GlassEnabled;
        var effectiveAlpha = alpha * ImGui.GetStyle().Alpha * (config.IsGratis ? UiMotion.BackdropAlpha : 1);
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
        if (config.IsGratis) {
            var anchored = ChromeLayout.Place("location", boxSize, config.LocationAnchor);
            if (anchored == Vector4.Zero) return;
            boxMin = ChromeLayout.Position(anchored);
        }
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
        if (config.IsGratis) RecordBounds(boxMin, boxMax, true);
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

    private static void DrawCompactTitle(string title) => BottomCardsDrawing.DrawLocation(title);

    public static void DrawCoordinates(Vector2? character, Vector2? cursor) => BottomCardsDrawing.DrawCoordinates(character, cursor);
}
