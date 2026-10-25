using System.Text.Json.Serialization;

namespace Gratis.Rendering;

public partial class MapDrawingConfig : IJsonOnDeserialized
{
    void IJsonOnDeserialized.OnDeserialized()
    {
        if (!ToolbarAnchorExplicit && ToolbarAnchor == ChromeAnchor.TopLeft)
            ToolbarAnchor = ChromeAnchor.TopCenter;
    }
}
