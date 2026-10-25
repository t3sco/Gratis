using System;
using System.Collections.Generic;
using System.Numerics;
using Gratis.Controllers;
using Gratis.Data;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class WindowPresetController
{
    private IMapWindowHost? host;
    private MoveWorkArea lastArea;
    private float lastScale;
    private bool observed;
    public void PrepareWindow(IMapWindowHost window, bool moving)
    {
        host = window;
        var config = window.Config;
        var settings = config.WindowPresetOptions;
        if (settings.Presets.Count == 0 && Pending == 0) return;
        var scale = MapDrawContext.Current.GlobalScale;
        var preset = settings.Find(Pending);
        var area = window.MoveWorkAreaAt(preset?.ScreenCenter ?? config.WindowPosition + config.WindowSize / 2);
        var busy = moving || ImGui.IsAnyMouseDown() || ImGui.IsAnyItemActive();
        var applied = ApplyPending(config, area, window.MinimumWindowSize, busy);
        if (!busy && !applied && (!observed || area != lastArea || scale != lastScale)) {
            var geometry = WindowPresetMath.Clamp(new(config.WindowPosition, config.WindowSize), area, window.MinimumWindowSize);
            config.WindowPosition = geometry.Position; config.WindowSize = geometry.Size;
            applied = true; Decision = "display-clamp";
        }
        if (applied) {
            ImGui.SetNextWindowPos(config.WindowPosition);
            ImGui.SetNextWindowSize(config.WindowSize);
            window.SaveWindowPosition();
        }
        if (!busy) { observed = true; lastArea = area; lastScale = scale; }
    }
    public bool SaveCurrent(SystemConfig config, string name = "")
    {
        var settings = config.WindowPresetOptions;
        if (settings.Presets.Count >= WindowPresetSettings.Limit) return false;
        var slot = 1;
        while (settings.Find(slot) != null) slot++;
        var center = config.WindowPosition + config.WindowSize / 2;
        var area = host?.MoveWorkAreaAt(center) ?? MoveHandleScreen.At(center);
        settings.Presets.Add(WindowPresetMath.Capture(slot, name, new(config.WindowPosition, config.WindowSize), area));
        Changed(); MapDrawContext.Current.SaveConfig();
        MapDrawContext.Current.ContextMenu.Notify($"Saved window preset {slot}");
        return true;
    }
    public IEnumerable<PlaceEntry> Commands(SystemConfig config)
    {
        foreach (var p in config.WindowPresetOptions.Presets)
            yield return new($"action:window:{p.Slot}", PlaceKind.Action, "Map: " + p.Name,
                $"Apply window preset {p.Slot}", 0, 0, Aliases: "window size position preset");
        if (config.WindowPresetOptions.Presets.Count < WindowPresetSettings.Limit)
            yield return new("action:window:save", PlaceKind.Action, "Save current as preset", "Save map window size and position", 0, 0, Aliases: "map window preset");
    }
    public bool Activate(string id, SystemConfig config)
    {
        if (!id.StartsWith("action:window:", StringComparison.Ordinal)) return false;
        var suffix = id[14..];
        if (suffix == "save") SaveCurrent(config);
        else if (int.TryParse(suffix, out var slot) && config.WindowPresetOptions.Find(slot) != null) Request(slot);
        return true;
    }
    public IReadOnlyList<ContextMenuItem> Menu(SystemConfig config)
    {
        var rows = new List<ContextMenuItem>();
        foreach (var p in config.WindowPresetOptions.Presets) {
            var slot = p.Slot;
            rows.Add(new(p.Name, GratisIcons.LayoutGrid, () => Request(slot)));
        }
        rows.Add(new("Save current as preset", GratisIcons.Plus,
            config.WindowPresetOptions.Presets.Count < WindowPresetSettings.Limit ? () => SaveCurrent(config) : null));
        return rows;
    }
}
