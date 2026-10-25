using System.Numerics;

namespace Gratis.Preview;

internal static class Terrain {
    public static nint Create(Textures textures, int size = 1024) {
        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++) {
            for (var x = 0; x < size; x++) {
                var height = Noise(x / 110f, y / 110f) * .65f + Noise(x / 34f, y / 34f) * .25f + Noise(x / 9f, y / 9f)
                    * .1f;
                var river = 490 + 90 * MathF.Sin(y / 132f) + 45 * MathF.Sin(y / 57f);
                var riverDistance = MathF.Abs(x - river);
                var road = 530 + 90 * MathF.Sin(x / 183f);
                var roadDistance = MathF.Abs(y - road);
                Vector3 color;
                if (riverDistance < 18)
                    color = new Vector3(82, 133, 143) + new Vector3(height * 25);
                else if (riverDistance < 24)
                    color = new Vector3(165, 167, 121);
                else if (roadDistance < 5)
                    color = new Vector3(207, 184, 130);
                else if (height > .68f)
                    color = new Vector3(134, 129, 100);
                else if (height > .51f)
                    color = new Vector3(149, 157, 105);
                else if (height > .35f)
                    color = new Vector3(173, 177, 119);
                else
                    color = new Vector3(188, 186, 133);
                if (riverDistance > 26 && roadDistance > 7) {
                    if (height * 24 % 1 < .07f)
                        color *= .87f;
                    var cellX = x / 16;
                    var cellY = y / 16;
                    var dx = x % 16 - 8;
                    var dy = y % 16 - 8;
                    if (Hash(cellX, cellY) > .72f && height < .55f && dx * dx + dy * dy < 21)
                        color = new Vector3(100, 130, 81) * (.9f + dy * .025f);
                }
                if (x % 128 == 0 || y % 128 == 0)
                    color *= .91f;
                color += new Vector3((Hash(x, y) - .5f) * 9);
                var index = (y * size + x) * 4;
                pixels[index] = (byte)Math.Clamp(color.X, 0, 255);
                pixels[index + 1] = (byte)Math.Clamp(color.Y, 0, 255);
                pixels[index + 2] = (byte)Math.Clamp(color.Z, 0, 255);
                pixels[index + 3] = 255;
            }
        }
        return textures.Add(size, size, pixels);
    }

    private static float Hash(int x, int y) {
        var value = unchecked((uint)x * 374761393u + (uint)y * 668265263u + 17);
        value = (value ^ value >> 13) * 1274126177u;
        return (value ^ value >> 16) / (float)uint.MaxValue;
    }

    private static float Noise(float x, float y) {
        var ix = (int)MathF.Floor(x);
        var iy = (int)MathF.Floor(y);
        var u = x - ix;
        var v = y - iy;
        u = u * u * (3 - 2 * u);
        v = v * v * (3 - 2 * v);
        return float.Lerp(float.Lerp(Hash(ix, iy), Hash(ix + 1, iy), u),
            float.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), u), v);
    }
}