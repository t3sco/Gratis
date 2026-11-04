using Gratis.Classes;

namespace Gratis.Rendering;

// Paint priority deliberately does not use IsProtected (which controls clustering).
public static class MarkerOrder {
    public static int Tier(MarkerInfo marker) {
        var family = MarkerIcon.Family(marker);
        if (family == MarkerIconFamily.Player) return 3;
        if (marker.IsFlag || family == MarkerIconFamily.Flag) return 2;
        if (marker.IsTarget || marker.IsTracked || marker.IsObjective || marker.IsSpotlighted
            || marker.MarkerType is MarkerType.Fate or MarkerType.Stellar
            || family is MarkerIconFamily.Quest or MarkerIconFamily.Leve
                or MarkerIconFamily.Fate or MarkerIconFamily.FateBoss or MarkerIconFamily.FateCollect
                or MarkerIconFamily.FateDefend or MarkerIconFamily.FateEscort or MarkerIconFamily.FateSpecial)
            return 1;
        return 0;
    }
}
