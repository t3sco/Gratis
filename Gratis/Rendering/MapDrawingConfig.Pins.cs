using System.Collections.Generic;

namespace Gratis.Rendering;

public enum PinLabelMode { OnHover, Always, Never }

public partial class MapDrawingConfig
{
    public bool Pins = true;
    public PinLabelMode PinLabels = PinLabelMode.OnHover;
    public int PinMaxPerMap = 200;
    public Dictionary<uint, List<UserPin>> PinData = [];
}
