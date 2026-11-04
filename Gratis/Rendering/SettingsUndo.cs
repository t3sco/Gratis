using Gratis.Data;
using global::System.Text.Json;

namespace Gratis.Rendering;

/// <summary>Snapshots only the data owned by destructive settings actions, never the whole config.</summary>
public static class SettingsUndo {
    private static readonly JsonSerializerOptions Json = new() { IncludeFields = true };

    private static bool Change<T>(SystemConfig config, string message, double now,
        global::System.Func<T> read, global::System.Action<T> write, global::System.Action change,
        global::System.Action? restored = null) {
        var before = JsonSerializer.Serialize(read(), Json);
        change();
        var after = JsonSerializer.Serialize(read(), Json);
        if (before == after) return false;
        UndoToast.Offer(config, message, now, () => JsonSerializer.Serialize(read(), Json) == after, () => {
            write(JsonSerializer.Deserialize<T>(before, Json)!);
            restored?.Invoke();
        });
        return true;
    }

    public static bool DeletePreset(SystemConfig config, int slot, double now, WindowPresetController controller) =>
        Change(config, "Window preset deleted", now, () => config.WindowPresetOptions,
            value => config.WindowPresets = value, () => { config.WindowPresetOptions.Delete(slot); }, controller.Changed);

    public static bool ResetPresets(SystemConfig config, double now, WindowPresetController controller) =>
        Change(config, "Window presets reset", now, () => config.WindowPresetOptions,
            value => config.WindowPresets = value, () => { config.WindowPresets = new(); controller.Cancel(); }, controller.Changed);

    public sealed class MarkerSnapshot {
        public MarkerSettings Markers = new();
        public int Style;
    }

    public static bool ResetMarkers(SystemConfig config, double now) =>
        Change(config, "Markers reset", now, () => new MarkerSnapshot { Markers = config.Markers, Style = config.MarkerStyle },
            value => { config.Markers = value.Markers; config.MarkerStyle = value.Style; },
            () => { config.ResetMarkers(); config.MarkerStyle = 1; });
}
