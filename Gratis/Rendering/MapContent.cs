using System;
using System.Numerics;

namespace Gratis.Rendering;

public readonly record struct MapContentBounds(Vector2 Min, Vector2 Max) {
    public Vector2 Size => Max - Min;
    public Vector2 Center => (Min + Max) / 2;
    public bool IsSmall(Vector2 texture) => Size.X > 0 && Size.Y > 0 && Size.X <= texture.X * .7f
        && Size.Y <= texture.Y * .7f && Size.X * Size.Y <= texture.X * texture.Y * .35f;
}

public static class MapContent {
    public static MapContentBounds? Scan(ReadOnlySpan<byte> rgba, int width, int height) {
        if (width < 8 || height < 8 || (long)width * height * 4 != rgba.Length)
            return null;
        Span<int> colours = stackalloc int[64];
        for (var i = 0; i < 32; i++) {
            var x = i * (width - 1) / 31;
            var y = i * (height - 1) / 31;
            var index = (i % 4) switch {
                0 => x,
                1 => (height - 1) * width + x,
                2 => y * width,
                _ => y * width + width - 1
            };
            var p = index * 4;
            colours[i] = (rgba[p] >> 4)
                | ((rgba[p + 1] >> 4) << 4)
                | ((rgba[p + 2] >> 4) << 8)
                | ((rgba[p + 3] >> 4) << 12);
        }
        var dominant = colours[0];
        var count = 0;
        for (var i = 0; i < 32; i++) {
            var matches = 0;
            for (var j = 0; j < 32; j++)
                if (colours[i] == colours[j])
                    matches++;
            if (matches > count) {
                count = matches;
                dominant = colours[i];
            }
        }
        if (count < 12)
            return null;
        var background = new Vector3((dominant & 15) * 16 + 8, ((dominant >> 4) & 15) * 16 + 8,
            ((dominant >> 8) & 15) * 16 + 8);
        var transparent = (dominant >> 12) < 2;
        var step = Math.Max(1, Math.Max(width, height) / 512);
        var min = new Vector2(width, height);
        var max = Vector2.Zero;
        var ink = 0;
        for (var y = 0; y < height; y += step)
            for (var x = 0; x < width; x += step) {
                var p = (y * width + x) * 4;
                if (rgba[p + 3] < 32 || !transparent
                    && Vector3.DistanceSquared(new(rgba[p], rgba[p + 1], rgba[p + 2]), background) < 48 * 48)
                    continue;
                min = Vector2.Min(min, new(x, y));
                max = Vector2.Max(max, new(x + step, y + step));
                ink++;
            }
        return ink < 16 ? null : new MapContentBounds(Vector2.Max(Vector2.Zero, min - new Vector2(step * 2)),
            Vector2.Min(new(width, height), max + new Vector2(step * 2)));
    }
}