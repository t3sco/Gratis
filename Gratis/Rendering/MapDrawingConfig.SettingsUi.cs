namespace Gratis.Rendering;

public partial class MapDrawingConfig {
    // Settings window preference, not part of any profile: shows rows marked Advanced. Search ignores it.
    public bool ShowAdvancedSettings = false;
    // The last changelog release whose What's new window was dismissed. Empty on configs from before the window.
    public string LastSeenChangelogVersion = "";
}
