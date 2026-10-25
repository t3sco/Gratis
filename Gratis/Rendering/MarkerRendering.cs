using System;
using System.Text;
using Gratis.Classes;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public enum MarkerDrawPath {
    GlyphPlate, PlateFallback, Shape, GameIcon, Dot, VectorPlate
}

public sealed class MarkerRendering {
    private readonly int[] counts = new int[6];
    private readonly Sample[] samples = new Sample[4];
    private readonly record struct Sample(uint Icon, MarkerIconFamily Family, char Glyph, float Pixels, bool Handle);
    private int frame = -1, sampleCount;
    private bool glyphs;
    private char missingGlyph;
    public int Count(MarkerDrawPath path) => counts[(int)path];
    public bool UseGlyphs {
        get {
            BeginFrame();
            return glyphs;
        }
    }

    public void BeginFrame() {
        var next = ImGui.GetFrameCount();
        if (frame == next)
            return;
        frame = next;
        Array.Clear(counts);
        sampleCount = 0;
        missingGlyph = '\0';
        glyphs = false;
        var context = MapDrawContext.Current;
        if (!context.Config.IsGratis || context.Config.Markers.IconStyle != MarkerIconStyle.Icons)
            return;
        var handle = context.GetIconFont(14 * context.GlobalScale);
        if (handle is null)
            return;
        using var font = handle.Push();
        var ranges = GratisIcons.GlyphRanges;
        for (var i = 0; i + 1 < ranges.Length; i += 2)
            if (!context.TryIconGlyphBounds((char)ranges[i], out _)) {
                missingGlyph = (char)ranges[i];
                return;
            }
        glyphs = true;
    }

    public void Record(MarkerDrawPath path, MarkerInfo marker, float pixels = 0) {
        BeginFrame();
        counts[(int)path]++;
        if (path != MarkerDrawPath.PlateFallback || sampleCount == samples.Length)
            return;
        var family = MarkerIcon.Family(marker);
        samples[sampleCount++] = new(marker.IconId, family, MarkerIcon.Glyph(family), pixels,
            MapDrawContext.Current.GetIconFont(pixels) is not null);
    }

    public string Diagnostics() {
        var context = MapDrawContext.Current;
        var message = new StringBuilder($"markers style={context.Config.Markers.IconStyle} "
            + $"visualStyle={context.Config.VisualStyle} frame={frame} glyph+plate={Count(MarkerDrawPath.GlyphPlate)} "
            + $"plate-fallback={Count(MarkerDrawPath.PlateFallback)} "
            + $"shape={Count(MarkerDrawPath.Shape)} game-icon={Count(MarkerDrawPath.GameIcon)} "
            + $"dot={Count(MarkerDrawPath.Dot)} vector-plate={Count(MarkerDrawPath.VectorPlate)} "
            + $"frameGlyphs={glyphs} missingGlyph=U+{(int)missingGlyph:X4}; {context.IconFontDiagnostics()}");
        for (var i = 0; i < sampleCount; i++) {
            var sample = samples[i];
            message.Append($"; fallback icon={sample.Icon} family={sample.Family} glyph=U+{(int)sample.Glyph:X4} "
                + $"pixels={sample.Pixels:0.##}->{TextDrawing.PixelSize(sample.Pixels)} "
                + $"handle={(sample.Handle ? "ready" : "unavailable/pending")}");
        }
        return message.ToString();
    }
}