using System.Numerics;

namespace Gratis.Rendering;

public static class GratisTheme
{
    public const float Grid = 4;
    public const float Inset = 14;
    public const float Gap = 8;
    public const float ControlHeight = 36;
    public const float ToolbarHeight = 44;
    public const float ToolbarCenterMargin = 32;
    public const float TextSize = 14;
    public const float SmallTextSize = 12;
    public const float IconSize = 18;
    public static readonly Vector4[] FilterTints = [
        new(1, .76f, .3f, 1), new(.96f, .86f, .61f, 1), new(.77f, .58f, 1, 1),
        new(.35f, .82f, 1, 1), new(.58f, .72f, 1, 1), new(.31f, .87f, .73f, 1),
        new(.87f, .75f, .6f, 1), new(.58f, .87f, .46f, 1), new(.98f, .82f, .36f, 1),
        new(.42f, .91f, .59f, 1), new(.75f, .78f, .82f, 1), new(.96f, .62f, .49f, 1),
    ];
    public const float AreaLabelSlant = .14f;
    public static readonly Vector4 WarmAccent = new(.85f, .72f, .48f, 1);
    public static readonly Vector4 Crystal = new(.72f, .9f, 1, 1);
    public static float ChromeScale => MapDrawContext.Current.GlobalScale *
        global::System.Math.Clamp(float.IsFinite(MapDrawContext.Current.Config.ChromeScale) ? MapDrawContext.Current.Config.ChromeScale : 1, .8f, 1.6f);
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
