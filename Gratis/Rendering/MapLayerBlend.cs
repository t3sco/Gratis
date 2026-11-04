namespace Gratis.Rendering;

internal sealed record MapLayerPixels(int Width, int Height, byte[] Rgba);

internal static class MapLayerBlend {
    private static bool Valid(MapLayerPixels layer) => layer.Width > 0 && layer.Height > 0
        && (long)layer.Width * layer.Height * 4 == layer.Rgba.LongLength;

    // An incompatible background must not turn a usable foreground into a blank map.
    public static MapLayerPixels? BlendOrForeground(MapLayerPixels background, MapLayerPixels foreground) {
        if (!Valid(foreground))
            return null;
        if (!Valid(background) || background.Width != foreground.Width || background.Height != foreground.Height)
            return foreground;
        var pixels = (byte[])background.Rgba.Clone();
        global::System.Threading.Tasks.Parallel.For(0, pixels.Length / 4, i => {
            var offset = i * 4;
            for (var channel = 0; channel < 3; channel++)
                pixels[offset + channel] = (byte)(pixels[offset + channel] * foreground.Rgba[offset + channel] / 255);
        });
        return new(background.Width, background.Height, pixels);
    }
}
