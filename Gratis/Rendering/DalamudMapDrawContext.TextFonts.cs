using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;

namespace Gratis.Rendering;

internal sealed partial class DalamudMapDrawContext {
    private readonly FontAtlasBudget geistBudget = new(FontAtlasBudget.MaximumTextHandles,
        FontAtlasBudget.MaximumArea / 2);

    private readonly global::System.Collections.Generic.Dictionary<IFontHandle, TextFont> textDrawFonts = [];

    private DefaultTextFont? defaultText;

    private bool fontFailureLogged;

    private int defaultFallbacks;

    private bool textSnap;

    private int textOversample = 1;

    private sealed class Font(IFontHandle handle) : IDrawFont {
        public IDisposable Push() => handle.Push();
    }

    private IDrawFont DefaultText => defaultText ??= new DefaultTextFont(this);

    private sealed class DefaultTextFont(DalamudMapDrawContext owner) : IDrawFont {
        public IDisposable Push() {
            owner.defaultFallbacks++;
            return Service.PluginInterface.UiBuilder.DefaultFontHandle.Push();
        }
    }

    private sealed class TextFont(DalamudMapDrawContext owner, IFontHandle handle) : IDrawFont {
        public string Validity = "not-checked";
        public int Fallbacks;
        public unsafe IDisposable Push() {
            IDisposable? pushed = null;
            try {
                using var locked = handle.TryLock(out _);
                if (locked is not null && handle.LoadException is null) {
                    pushed = handle.Push();
                    if (ImGui.GetFont().Handle == locked.ImFont.Handle && DrawBinding.ValidTextFont(ImGui.GetFont())) {
                        Validity = "valid";
                        return pushed;
                    }
                    pushed.Dispose();
                    pushed = null;
                    Validity = "invalid-font/texture/ASCII";
                }
                else
                    Validity = "pending/failed";
            }
            catch (Exception error) {
                pushed?.Dispose();
                Validity = "failed:" + error.GetType().Name;
                owner.LogFontFailure(error);
            }
            Fallbacks++;
            return owner.DefaultText.Push();
        }
    }

    private IDrawFont GuardText(IFontHandle? handle) {
        if (handle is null)
            return DefaultText;
        if (!textDrawFonts.TryGetValue(handle, out var cached))
            textDrawFonts[handle] = cached = new TextFont(this, handle);
        return cached;
    }

    public override IDisposable PushDefaultTextFont() => DefaultText.Push();

    public override IDrawFont? GetTitleFont(int choice, float size) {
        if (Config.IsGratis)
            return GetChromeFont(size, ChromeFontRole.Body);
        try {
            var handle = GetNativeTitleFont(choice, size);
            return handle is { Available: true } ? new Font(handle) : null;
        }
        catch (Exception error) { LogFontFailure(error); return null; }
    }

    public override IDrawFont? GetChromeFont(float size, ChromeFontRole role) {
        if (!Config.IsGratis)
            return null;
        try {
            return GetChromeFontCore(size);
        }
        catch (Exception error) {
            LogFontFailure(error);
            return DefaultText;
        }
    }

    private void LogFontFailure(Exception error) {
        if (fontFailureLogged)
            return;
        fontFailureLogged = true;
        Service.Log.Warning(error, "Sized text unavailable; using the default Dalamud UI font.");
    }

    private IDrawFont GetChromeFontCore(float size) {
        var pixels = TextDrawing.PixelSize(size);
        var key = (10, pixels);
        if (!titleFonts.TryGetValue(key, out var handle)) {
            if (!geistBudget.Reserve(key.Item1, pixels, FontAtlasBudget.TextGlyphs))
                return DefaultText;
            handle = CreateTextFont(pixels);
            titleFonts[key] = handle;
        }
        return GuardText(handle);
    }

    private IFontHandle CreateTextFont(int pixels) {
        const string filename = "Geist-Regular.ttf";
        var snap = Config.TextPixelSnap;
        var oversample = Math.Clamp(Config.TextOversampleH, 1, 2);
        textSnap = snap;
        textOversample = oversample;
        return Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(toolkit => {
            using var stream = typeof(DalamudMapDrawContext).Assembly.GetManifestResourceStream(
                $"Gratis.Assets.Fonts.{filename}")
                ?? throw new global::System.IO.FileNotFoundException(filename);
            toolkit.Font = toolkit.AddFontFromStream(stream, new SafeFontConfig {
                SizePx = pixels,
                OversampleH = oversample,
                OversampleV = 1,
                PixelSnapH = snap,
                GlyphRanges = FontAtlasBudget.TextRanges
            }, false, filename);
            toolkit.AddDalamudAssetFont(global::Dalamud.DalamudAsset.NotoSansCjkMedium, new SafeFontConfig {
                SizePx = pixels,
                OversampleH = oversample,
                OversampleV = 1,
                PixelSnapH = snap,
                MergeFont = toolkit.Font,
                GlyphRanges = FontAtlasBudget.FallbackRanges,
            });
            toolkit.AddGameGlyphs(new global::Dalamud.Interface.GameFonts.GameFontStyle {
                FamilyAndSize = global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Axis14,
                SizePx = pixels,
            }, FontAtlasBudget.GameSymbolRanges, toolkit.Font);
        }));
    }

    public override string TextDiagnostics() {
        var message = new global::System.Text.StringBuilder(base.TextDiagnostics());
        long iconArea = 0;
        foreach (var size in iconFonts.Keys)
            iconArea += FontAtlasBudget.Estimate(size, GratisIcons.GlyphRanges.Length / 2);
        var totalArea = geistBudget.EstimatedArea + iconArea;
        message.Append($" handles={geistBudget.Count}/{FontAtlasBudget.MaximumTextHandles} "
            + $"allOwnedHandles={titleFonts.Count + iconFonts.Count} "
            + $"estimatedArea={totalArea}/{FontAtlasBudget.MaximumArea}px "
            + $"textArea={geistBudget.EstimatedArea} iconArea={iconArea} "
            + $"budgetWarning={geistBudget.Denied > 0 || totalArea > FontAtlasBudget.MaximumArea / 2} "
            + $"denied={geistBudget.Denied} iconLegacyDenied={fontBudget.Denied} "
            + $"defaultFallbacks={defaultFallbacks} oversample={textOversample}x1 pixelSnapH={textSnap} neverEvict=true");
        foreach (var pair in titleFonts) {
            textDrawFonts.TryGetValue(pair.Value, out var font);
            var ranges = pair.Key.Choice == 1 ? "legacy" : pair.Key.Choice is >= 2 and <= 5 ? "game-bitmap"
                : "0020-00FF,0300-03FF,2000-206F,221E,2502,2523,2528,2573,E0BE-E0BF";
            message.Append($"; text({pair.Key.Choice},{pair.Key.Size}) built={pair.Value.Available} "
                + $"loadError={pair.Value.LoadException?.GetType().Name ?? "none"} ranges={ranges} "
                + $"validity={font?.Validity ?? "not-checked"} fallbacks={font?.Fallbacks ?? 0}");
        }
        return message.Append("; ").Append(IconFontDiagnostics()).ToString();
    }
}
