namespace Gratis.Rendering;

public partial class MapDrawingConfig {
    // Defaults to done so any config already on disk, even a bare {}, never starts Quick setup.
    // Only a first-ever run with no config file clears it, through MarkFreshInstall.
    public bool QuickSetupDone = true;
    public void MarkFreshInstall() => QuickSetupDone = false;
}
