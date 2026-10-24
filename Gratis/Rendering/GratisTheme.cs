using System.Numerics;

namespace Gratis.Rendering;

public static class GratisTheme
{
    public const float Grid = 4;
    public const float CardRadius = 8;
    public const float ButtonRadius = 6;
    public const float HoverSeconds = .09f;
    public static readonly Vector4 Surface = new(10 / 255f, 10 / 255f, 11 / 255f, .85f);
    public static readonly Vector4 Border = new(1, 1, 1, .10f);
    public static readonly Vector4 Primary = new(237 / 255f, 237 / 255f, 237 / 255f, 1);
    public static readonly Vector4 Secondary = new(143 / 255f, 143 / 255f, 143 / 255f, 1);
    public static readonly Vector4 Accent = new(0, 112 / 255f, 243 / 255f, 1);
    public static readonly Vector4 Outline = new(0, 0, 0, .35f);
    public static readonly Vector4 Poi = new(1, 1, 1, 1);
    public static readonly Vector4 Transport = new(.20f, .75f, 1, 1);
    public static readonly Vector4 Objective = new(1, .69f, .12f, 1);
    public static readonly Vector4 Link = new(.72f, .42f, 1, 1);
    public static readonly Vector4 Service = new(.08f, .88f, .72f, 1);
    public static readonly Vector4 Party = new(.35f, .95f, .42f, 1);
}
