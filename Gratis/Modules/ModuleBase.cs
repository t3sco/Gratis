using Gratis.Classes;

namespace Gratis.Modules;

public abstract class ModuleBase {
    public abstract bool ProcessMarker(MarkerInfo markerInfo);
}