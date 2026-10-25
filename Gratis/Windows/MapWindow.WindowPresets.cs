using Gratis.Compat.Extensions;

namespace Gratis.Windows;

public partial class MapWindow {
    public string WindowPresetDiagnostics() =>
        Rendering.MapDrawContext.Current.WindowPresets.Diagnostics(System.SystemConfig,
            Service.Condition.IsBoundByDuty());
}