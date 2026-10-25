using System;
using System.Collections.Generic;
using System.Numerics;

namespace Gratis.Rendering;

public sealed class FontAtlasBudget
{
    public const int MaximumHandles = 16;
    public const long MaximumArea = 4 * 1024 * 1024;
    public static readonly ushort[] TextRanges = [0x20, 0x024f, 0x2000, 0x206f, 0];
    public const int TextGlyphs = 672;
    private readonly HashSet<(int Face, int Size)> reserved = [];
    public int Count => reserved.Count;
    public long EstimatedArea { get; private set; }
    public int Denied { get; private set; }
    public static long Estimate(int size, int glyphs) => (long)(size + 4) * (size + 4) * glyphs;
    public bool Reserve(int face, int size, int glyphs)
    {
        if (reserved.Contains((face, size))) return false;
        var area = Estimate(size, glyphs);
        if (Count >= MaximumHandles || area > MaximumArea - EstimatedArea) { Denied++; return false; }
        reserved.Add((face, size)); EstimatedArea += area;
        return true;
    }
    public static bool ValidGlyph(float size, Vector4 bounds, Vector4 uv) =>
        float.IsFinite(size) && size > 0 && size <= 512 &&
        float.IsFinite(bounds.X + bounds.Y + bounds.Z + bounds.W + uv.X + uv.Y + uv.Z + uv.W) &&
        bounds.Z > bounds.X && bounds.W > bounds.Y && bounds.Z - bounds.X <= size * 2 && bounds.W - bounds.Y <= size * 2 &&
        uv.X >= 0 && uv.Y >= 0 && uv.Z <= 1 && uv.W <= 1 && uv.Z > uv.X && uv.W > uv.Y;
}
