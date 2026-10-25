namespace Gratis.Rendering;

public enum LocationCardContent { AreaOnly, Zone, ZoneAndArea, RegionZoneArea }
public enum CoordinateContent { Player, Cursor, Both }

public partial class MapDrawingConfig
{
    public LocationCardContent LocationCardShows = LocationCardContent.ZoneAndArea;
    public CoordinateContent CoordinatesShow = CoordinateContent.Both;
    public bool LocationAnchorExplicit;
    public bool CoordinateAnchorExplicit;
    public void ResetBottomCards()
    {
        LocationCardShows = LocationCardContent.ZoneAndArea;
        CoordinatesShow = CoordinateContent.Both;
        LocationAnchor = CoordinateAnchor = ChromeAnchor.BottomCenter;
        LocationAnchorExplicit = CoordinateAnchorExplicit = false;
    }
}
