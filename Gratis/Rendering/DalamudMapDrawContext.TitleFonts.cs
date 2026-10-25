using System;
using Dalamud.Interface.ManagedFontAtlas;
using GameFont = global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize;
using Math = global::System.Math;
using MathF = global::System.MathF;

namespace Gratis.Rendering;

internal sealed partial class DalamudMapDrawContext {
    private IFontHandle? classicAxis;

    private readonly FontAtlasBudget fontBudget = new();

    private readonly global::System.Collections.Generic.Dictionary<(int Choice, int Size), IFontHandle> titleFonts = [];

    private static readonly GameFont[][] GameFontPresets = [
        [GameFont.Axis96, GameFont.Axis12, GameFont.Axis14, GameFont.Axis18, GameFont.Axis36],
        [GameFont.Meidinger16, GameFont.Meidinger20, GameFont.Meidinger40],
        [GameFont.TrumpGothic184, GameFont.TrumpGothic23, GameFont.TrumpGothic34, GameFont.TrumpGothic68],
        [GameFont.Jupiter16, GameFont.Jupiter20, GameFont.Jupiter23, GameFont.Jupiter45, GameFont.Jupiter90],
    ];

    public override IDisposable PushLargeFont() {
        if (Config.IsGratis)
            return GetChromeFont(24, ChromeFontRole.Body)!.Push();
        classicAxis ??= Service.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(
            new global::Dalamud.Interface.GameFonts.GameFontStyle {
                SizePt = 72,
                FamilyAndSize = GameFont.Axis36, Italic = true, BaseSkewStrength = 16, });
        return classicAxis.Push();
    }

    private IFontHandle? GetNativeTitleFont(int fontChoice, float sizePx) {
        if (fontChoice <= 0)
            return null;

        if (fontChoice == 1) {
            var snap = Config.IsGratis;
            var size = snap ? TextDrawing.PixelSize(sizePx) : Math.Max(8, (int)(sizePx + 0.5f));
            var notoKey = (snap ? 20 : 1, size);
            if (!titleFonts.TryGetValue(notoKey, out var notoHandle)) {
                if (!fontBudget.Reserve(notoKey.Item1, size, FontAtlasBudget.TextGlyphs))
                    return null;
                notoHandle = CreateNotoFont(size, snap);
                titleFonts[notoKey] = notoHandle;
            }

            return notoHandle;
        }

        var presets = GameFontPresets[fontChoice - 2];
        var best = presets[0];
        var bestDistance = float.MaxValue;
        foreach (var preset in presets) {
            var distance = MathF.Abs(new global::Dalamud.Interface.GameFonts.GameFontStyle(preset).SizePx - sizePx);
            if (distance < bestDistance) {
                bestDistance = distance;
                best = preset;
            }
        }

        var key = (fontChoice, (int)best);
        if (!titleFonts.TryGetValue(key, out var handle)) {
            if (!fontBudget.Reserve(fontChoice, (int)best, 0))
                return null;
            handle = Service.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(
                new global::Dalamud.Interface.GameFonts.GameFontStyle(best));
            titleFonts[key] = handle;
        }

        return handle;
    }

    private IFontHandle CreateNotoFont(int size, bool snap) =>
        Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(toolkit => {
            var config = new SafeFontConfig { SizePx = size };
            if (snap) {
                config.GlyphRanges = FontAtlasBudget.TextRanges;
                config.OversampleH = 1;
                config.OversampleV = 1;
                config.PixelSnapH = true;
            }
            toolkit.Font = toolkit.AddDalamudAssetFont(global::Dalamud.DalamudAsset.NotoSansCjkMedium, config);
        }));
}
