using System.Numerics;

namespace Gratis.Data;

public class IconSetting
{
    public required uint IconId { get; set; }
    public bool Hide;
    public bool AllowTooltip = true;
    public float Scale = 1.0f;
    public bool AllowClick = true;
    public Vector4 Color = Vector4.One;

    public void Reset()
    {
        Hide = false;
        AllowTooltip = true;
        Scale = 1.0f;
        AllowClick = true;
        Color = Vector4.One;
    }
}
