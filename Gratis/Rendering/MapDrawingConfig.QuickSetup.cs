namespace Gratis.Rendering;

public partial class MapDrawingConfig {
    // Defaults to done so any config already on disk, even a bare {}, never starts Quick setup.
    // Only a first-ever run with no config file clears it, through MarkFreshInstall.
    public bool QuickSetupDone = true;
    public void MarkFreshInstall() {
        QuickSetupDone = false;
        // New installs start with the arrow and cone following the camera, like the game's map. The field default
        // stays Character so a saved file that predates the setting keeps the behaviour its user already had.
        ArrowPoints = PlayerArrowDirection.Camera;
        LastSeenChangelogVersion = Changelog.Latest;
    }
}
