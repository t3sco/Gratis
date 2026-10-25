using System.Text.Json.Serialization;

namespace Gratis.Rendering;

public partial class MapDrawingConfig : IJsonOnDeserialized
{
    void IJsonOnDeserialized.OnDeserialized()
    {
        if (!ToolbarAnchorExplicit && ToolbarAnchor == ChromeAnchor.TopLeft)
            ToolbarAnchor = ChromeAnchor.TopCenter;
        if (!LocationAnchorExplicit && LocationAnchor == ChromeAnchor.BottomRight) LocationAnchor = ChromeAnchor.BottomCenter;
        if (!CoordinateAnchorExplicit && CoordinateAnchor == ChromeAnchor.BottomRight) CoordinateAnchor = ChromeAnchor.BottomCenter;
    }
}
