using System.Collections.Generic;

namespace Gratis.Rendering;

public partial class MapDrawingConfig {
    public bool RememberMapView = true;
    public bool RememberMapLens = false;
    public List<RememberedMapView> MapViews = [];
}