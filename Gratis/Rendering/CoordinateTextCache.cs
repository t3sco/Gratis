using System;
using System.Globalization;
using System.Numerics;

namespace Gratis.Rendering;

internal sealed class CoordinateTextCache {
    private int x, y;
    private string text = "--.- / --.-";
    private bool hasValue;

    public string Format(Vector2? value) {
        if (value is not { } point) {
            hasValue = false;
            return "--.- / --.-";
        }
        var nextX = BitConverter.SingleToInt32Bits(point.X);
        var nextY = BitConverter.SingleToInt32Bits(point.Y);
        if (!hasValue || x != nextX || y != nextY) {
            x = nextX;
            y = nextY;
            hasValue = true;
            text = point.X.ToString("0.0", CultureInfo.InvariantCulture).PadLeft(4) + " / "
                + point.Y.ToString("0.0", CultureInfo.InvariantCulture).PadRight(4);
        }
        return text;
    }
}
