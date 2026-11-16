namespace Gratis.Rendering;

public enum LocationCardContent {
    AreaOnly, Zone, ZoneAndArea, RegionZoneArea
}
public enum LocationPlacement {
    Floating, InToolbar
}
public enum CoordinateContent {
    Player, Cursor, Both
}

public partial class MapDrawingConfig {
    public LocationCardContent LocationCardShows = LocationCardContent.ZoneAndArea;
    public CoordinateContent CoordinatesShow = CoordinateContent.Both;
    /// <summary>Floating keeps the location as its own card or title; InToolbar writes it into the toolbar row.</summary>
    public LocationPlacement LocationPlacement = LocationPlacement.Floating;
    public bool LocationAnchorExplicit;
    public bool CoordinateAnchorExplicit;
    public void ResetBottomCards() {
        LocationCardShows = LocationCardContent.ZoneAndArea;
        CoordinatesShow = CoordinateContent.Both;
        LocationPlacement = LocationPlacement.Floating;
        LocationAnchor = CoordinateAnchor = ChromeAnchor.BottomCenter;
        LocationAnchorExplicit = CoordinateAnchorExplicit = false;
    }
}