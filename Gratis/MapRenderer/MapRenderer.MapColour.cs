using System;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Utility;
using Gratis.Rendering;

namespace Gratis.MapRenderer;

public unsafe partial class MapRenderer {
    private readonly MapColourTextureCache mapColours = new();

    private void DrawColouredMapImage(IDalamudTextureWrap original, string source, Func<MapColourPixels> load) {
        var settings = System.SystemConfig.MapColour ??= new MapColourSettings();
        var coloured = mapColours.Get(source, original.Handle, settings.Parameters, load);
        DrawMapImage(coloured ?? original);
    }

    private static MapColourPixels LoadMapPixels(string foregroundPath, string? backgroundPath = null) {
        var foreground = GetTexFile(foregroundPath) ?? throw new InvalidOperationException($"Cannot load map {foregroundPath}");
        var fg = foreground.GetRgbaImageData();
        if (backgroundPath is null) return new MapColourPixels(foreground.Header.Width, foreground.Header.Height, fg);
        var background = GetTexFile(backgroundPath) ?? throw new InvalidOperationException($"Cannot load map {backgroundPath}");
        var bg = background.GetRgbaImageData();
        if (background.Header.Width != foreground.Header.Width || background.Header.Height != foreground.Header.Height)
            throw new InvalidOperationException("Map layer dimensions differ.");
        for (var i = 0; i < bg.Length; i += 4) {
            bg[i] = (byte)(bg[i] * fg[i] / 255);
            bg[i + 1] = (byte)(bg[i + 1] * fg[i + 1] / 255);
            bg[i + 2] = (byte)(bg[i + 2] * fg[i + 2] / 255);
        }
        return new MapColourPixels(background.Header.Width, background.Header.Height, bg);
    }
}