using System;
using Dalamud.Interface.ManagedFontAtlas;
using Math = global::System.Math;

namespace Gratis.Rendering;

internal sealed partial class DalamudMapDrawContext {
    private readonly global::System.Collections.Generic.Dictionary<int, IFontHandle> iconFonts = [];

    private readonly global::System.Collections.Generic.Dictionary<int, Font> iconDrawFonts = [];

    private bool iconFontFailed;

    public override IDrawFont? GetIconFont(float size) {
        if (iconFontFailed)
            return null;
        var pixels = TextDrawing.IconPixelSize(size);
        if (!iconFonts.ContainsKey(pixels)) {
            if (!fontBudget.Reserve(100, pixels, GratisIcons.GlyphRanges.Length / 2))
                return NearestIconFont(pixels);
            try {
                CreateIconFont(pixels);
            }
            catch (Exception error) {
                iconFontFailed = true;
                Service.Log.Warning(error, "Icon atlas unavailable; retaining contextual vector plates.");
                return null;
            }
        }
        return NearestIconFont(pixels);
    }

    private IDrawFont? NearestIconFont(int pixels) {
        var nearest = -1;
        foreach (var entry in iconFonts)
            if (entry.Value.Available && (nearest < 0 || Math.Abs(entry.Key - pixels) < Math.Abs(nearest - pixels)))
                nearest = entry.Key;
        return !iconFontFailed && nearest >= 0 ? iconDrawFonts[nearest] : null;
    }

    private void CreateIconFont(int pixels) {
        var handle = Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(toolkit => {
            try {
                using var stream = typeof(DalamudMapDrawContext).Assembly.GetManifestResourceStream(
                    "Gratis.Assets.Fonts.Icons.icons.ttf")
                    ?? throw new global::System.IO.FileNotFoundException("Icons");
                toolkit.AddFontFromStream(stream,
                    new SafeFontConfig {
                        SizePx = pixels, OversampleH = 1, OversampleV = 1, PixelSnapH = true,
                        GlyphRanges = GratisIcons.GlyphRanges
                    },
                    false, "Icons");
            }
            catch (Exception error) {
                iconFontFailed = true;
                Service.Log.Warning(error, "Icon font unavailable; using primitive icons.");
            }
        }));
        iconFonts[pixels] = handle;
        iconDrawFonts[pixels] = new Font(handle);
    }

    public override string IconFontDiagnostics() {
        var text = new global::System.Text.StringBuilder($"icon-fonts failed={iconFontFailed} built=[");
        foreach (var size in TextDrawing.IconSizes)
            if (iconFonts.TryGetValue(size, out var handle))
                text.Append($"{size}:{(handle.Available ? "ready" : "pending")},");
        return text.Append(']').ToString();
    }
}