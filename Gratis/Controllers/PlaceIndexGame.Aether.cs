namespace Gratis.Controllers;

public sealed partial class PlaceIndexGame {
    // Shown on the location card hover; empty when the zone has no field currents, is complete, or is not signed in.
    public string AetherHint => AetherCurrentsGame.ZoneLine();
}
