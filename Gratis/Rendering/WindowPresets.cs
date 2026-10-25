using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Gratis.Data;

namespace Gratis.Rendering;

public sealed class WindowPreset
{
    public int Slot;
    public string Name = "";
    public Vector2 Position;
    public Vector2 Size;
    public Vector2 ScreenCenter;
}

public sealed class WindowPresetSettings
{
    public const int Limit = 5;
    public List<WindowPreset> Presets = [];
    public int OpenAt;
    public int DutyOpenAt;

    public WindowPreset? Find(int slot)
    {
        if (Presets != null) foreach (var preset in Presets) if (preset != null && preset.Slot == slot) return preset;
        return null;
    }
    public int ForContext(bool inDuty) => inDuty && Find(DutyOpenAt) != null ? DutyOpenAt : OpenAt;
    public void Normalize()
    {
        Presets ??= [];
        var seen = 0;
        for (var i = 0; i < Presets.Count;) {
            var p = Presets[i];
            if (p == null || p.Slot is < 1 or > Limit || (seen & (1 << p.Slot)) != 0 ||
                !WindowPresetMath.Finite(p.Position) || !WindowPresetMath.Finite(p.Size) || p.Size.X <= 0 || p.Size.Y <= 0) {
                Presets.RemoveAt(i); continue;
            }
            seen |= 1 << p.Slot;
            if (!WindowPresetMath.Finite(p.ScreenCenter)) p.ScreenCenter = Vector2.Zero;
            if (UnsafeName(p.Name))
                p.Name = CleanName(p.Name, p.Slot);
            i++;
        }
        if (Find(OpenAt) == null) OpenAt = 0;
        if (Find(DutyOpenAt) == null) DutyOpenAt = 0;
    }
    private static bool UnsafeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 40) return true;
        foreach (var c in name) if (char.IsControl(c) || char.GetUnicodeCategory(c) == global::System.Globalization.UnicodeCategory.Format) return true;
        return false;
    }
    public static string CleanName(string? name, int slot)
    {
        var text = new StringBuilder();
        foreach (var c in name ?? "") {
            if (char.IsControl(c) || char.GetUnicodeCategory(c) == global::System.Globalization.UnicodeCategory.Format) continue;
            if (text.Length == 40) break;
            text.Append(c);
        }
        if (text.Length > 0 && char.IsHighSurrogate(text[^1])) text.Length--;
        var result = text.ToString().Trim();
        return result.Length == 0 ? $"Preset {slot}" : result;
    }
    public bool Delete(int slot)
    {
        var removed = Presets.RemoveAll(p => p.Slot == slot) > 0;
        if (OpenAt == slot) OpenAt = 0;
        if (DutyOpenAt == slot) DutyOpenAt = 0;
        return removed;
    }
}

public readonly record struct WindowGeometry(Vector2 Position, Vector2 Size);

public static class WindowPresetMath
{
    public static bool Finite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
    public static WindowGeometry Clamp(WindowGeometry geometry, MoveWorkArea area, Vector2 minimum)
    {
        var available = Vector2.Max(Vector2.One, area.Max - area.Min);
        var size = Finite(geometry.Size) ? geometry.Size : minimum;
        size = Vector2.Clamp(size, Vector2.Min(minimum, available), available);
        var position = Finite(geometry.Position) ? geometry.Position : area.Min;
        return new(Vector2.Clamp(position, area.Min, area.Min + available - size), size);
    }
    public static WindowPreset Capture(int slot, string name, WindowGeometry geometry, MoveWorkArea area)
    {
        geometry = Clamp(geometry, area, Vector2.One);
        var available = Vector2.Max(Vector2.One, area.Max - area.Min);
        return new() { Slot = slot, Name = WindowPresetSettings.CleanName(name, slot),
            Position = (geometry.Position - area.Min) / available, Size = geometry.Size / available,
            ScreenCenter = (area.Min + area.Max) / 2 };
    }
    public static WindowGeometry Restore(WindowPreset preset, MoveWorkArea area, Vector2 minimum) =>
        Clamp(new(area.Min + preset.Position * (area.Max - area.Min), preset.Size * (area.Max - area.Min)), area, minimum);
}

public sealed partial class WindowPresetController
{
    public int Pending { get; private set; }
    public int LastApplied { get; private set; }
    public int Revision { get; private set; }
    public string Decision { get; private set; } = "last-position";
    public Action? ShowWindow { get; set; }
    public void Changed() => Revision++;
    public void OnOpen(WindowPresetSettings settings, bool inDuty)
    {
        settings.Normalize();
        if (Pending == 0) { Pending = settings.ForContext(inDuty); Decision = Pending == 0 ? "last-position" : "queued-on-open"; }
    }
    public void Request(int slot) { Pending = slot; Decision = slot == 0 ? "last-position" : "queued"; if (slot != 0) ShowWindow?.Invoke(); }
    public void Cancel() { Pending = 0; Decision = "closed"; }
    public bool ApplyPending(SystemConfig config, MoveWorkArea area, Vector2 minimum, bool busy)
    {
        if (Pending == 0) return false;
        if (busy) { Decision = "waiting-for-release"; return false; }
        var preset = config.WindowPresetOptions.Find(Pending);
        Pending = 0;
        if (preset == null) { Decision = "missing-preset"; return false; }
        var geometry = WindowPresetMath.Restore(preset, area, minimum);
        config.WindowPosition = geometry.Position; config.WindowSize = geometry.Size;
        LastApplied = preset.Slot; Decision = "applied";
        return true;
    }
    public string Diagnostics(SystemConfig config, bool inDuty) =>
        $"window presets={config.WindowPresetOptions.Presets.Count}/5 openAt={config.WindowPresetOptions.OpenAt} dutyOpenAt={config.WindowPresetOptions.DutyOpenAt} inDuty={inDuty} selected={config.WindowPresetOptions.ForContext(inDuty)} pending={Pending} applied={LastApplied} decision={Decision} position={config.WindowPosition} size={config.WindowSize}";
}
