using System.Text.Json.Serialization;

namespace Gratis.Rendering;

public partial class MapDrawingConfig
{
    public WindowPresetSettings WindowPresets = new();
    [JsonIgnore] public WindowPresetSettings WindowPresetOptions {
        get { WindowPresets ??= new(); WindowPresets.Normalize(); return WindowPresets; }
    }
}
