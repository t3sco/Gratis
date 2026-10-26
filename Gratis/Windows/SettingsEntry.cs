using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Gratis.Data;
using Gratis.Rendering;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Windows;

public sealed class SettingsEntry {
    public string Section { get; }
    public string Group { get; }
    public string Path { get; }
    public string Label { get; }
    public string? Tooltip { get; }
    public Func<SystemConfig, bool> Visible { get; }
    public Func<SystemConfig, bool> Enabled { get; }
    private readonly FieldInfo[] fields;
    private readonly float minimum, maximum;
    private readonly string format;
    private readonly string[]? choices;
    private string[]? enumNames;

    public SettingsEntry(string section, string group, string path, string label, float min = 0, float max = 1,
        string format = "%.2f", string? choices = null, Func<SystemConfig, bool>? visible = null, Func<SystemConfig,
        bool>? enabled = null, string? tip = null) {
        Section = section;
        Group = group;
        Path = path;
        Label = label;
        Tooltip = tip;
        minimum = min;
        maximum = max;
        this.format = format;
        this.choices = choices?.Split('|');
        Visible = visible ?? (_ => true);
        Enabled = enabled ?? (_ => true);
        var type = typeof(SystemConfig);
        fields = path.Split('.').Select(name => {
            var field = type.GetField(name) ?? throw new ArgumentException($"Unknown setting {path}.");
            type = field.FieldType;
            return field;
        }).ToArray();
    }

    public bool Matches(string search) => string.IsNullOrWhiteSpace(search)
        || search.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word =>
            Label.Contains(word, StringComparison.OrdinalIgnoreCase)
            || Path.Contains(word, StringComparison.OrdinalIgnoreCase)
            || Group.Contains(word, StringComparison.OrdinalIgnoreCase)
            || Section.Contains(word, StringComparison.OrdinalIgnoreCase)
            || SettingsLayout.RowLabel(this).Contains(word, StringComparison.OrdinalIgnoreCase)
            || SettingsLayout.GroupLabel(Group).Contains(word, StringComparison.OrdinalIgnoreCase)
            || SettingsLayout.SectionLabel(Section).Contains(word, StringComparison.OrdinalIgnoreCase));

    private object Owner(SystemConfig config) {
        object owner = config;
        foreach (var field in fields[..^1])
            owner = field.GetValue(owner)!;
        return owner;
    }

    public object Value(SystemConfig config) => fields[^1].GetValue(Owner(config))!;
    public void Set(SystemConfig config, object value) {
        fields[^1].SetValue(Owner(config), value);
        if (Path == "ToolbarAnchor")
            config.ToolbarAnchorExplicit = true;
        if (Path == "LocationAnchor")
            config.LocationAnchorExplicit = true;
        if (Path == "CoordinateAnchor")
            config.CoordinateAnchorExplicit = true;
    }

    public void Reset(SystemConfig config, SystemConfig defaults) {
        Set(config, Value(defaults));
        if (Path == "ToolbarAnchor")
            config.ToolbarAnchorExplicit = defaults.ToolbarAnchorExplicit;
        if (Path == "LocationAnchor")
            config.LocationAnchorExplicit = defaults.LocationAnchorExplicit;
        if (Path == "CoordinateAnchor")
            config.CoordinateAnchorExplicit = defaults.CoordinateAnchorExplicit;
    }

    public bool Draw(SystemConfig config) {
        if (Path == "WindowPresets")
            return SettingsWindowPresets.Draw(config);
        if (Path == "ActiveLens")
            return Rendering.LensDrawing.DrawSelector(config);
        if (Path == "CustomLenses")
            return Rendering.LensDrawing.DrawEditor(config);
        if (Path == "LensCycleKey")
            return Rendering.LensDrawing.DrawKeyBinding(config);
        var value = Value(config);
        var changed = false;
        switch (value) {
            case bool flag:
                changed = config.IsGratis ? SettingsTheme.Toggle("##value", ref flag, config.AccentColor)
                    : ImGui.Checkbox("##value", ref flag);
                value = flag;
                break;
            case float number:
                var sizeScale = Path == "FlatMarkerSize" ? MathF.Max(config.IconScale / .5f, .001f) : 1;
                number *= sizeScale;
                changed = config.IsGratis
                    ? SettingsTheme.Slider("##value", ref number, minimum, maximum, format, config.AccentColor)
                    : ImGui.SliderFloat("##value", ref number, minimum, maximum, format);
                value = number / sizeScale;
                break;
            case int number:
                if (config.IsGratis && choices is null) {
                    float numeric = number;
                    changed = SettingsTheme.Slider("##value", ref numeric, minimum, maximum, "%d", config.AccentColor,
                        true);
                    number = (int)numeric;
                }
                else
                    changed = choices is null ? ImGui.SliderInt("##value", ref number, (int)minimum, (int)maximum)
                        : config.IsGratis ? ChromeControls.Combo("##value", ref number, choices, SettingsTheme.Scale)
                        : ImGui.Combo("##value", ref number, choices, choices.Length);
                value = number;
                break;
            case FadeMode mode:
                var selected = (int)mode;
                var summary = selected == 0 ? "Never" : (selected & 1) != 0 ? "Always" : string.Join(", ", new[] {
                        (4, "Moving"),
                        (8, "Focused"),
                        (16, "Unfocused") }.Where(item => (selected & item.Item1) != 0).Select(item => item.Item2));
                if (ImGui.BeginCombo("##value", summary)) {
                    foreach (var (bit, name) in new[] { (1, "Always"), (4, "When moving"), (8, "When focused"),
                        (16, "When unfocused") }) {
                        var on = (selected & bit) != 0;
                        var toggled = config.IsGratis
                            ? SettingsTheme.Toggle("##" + name, ref on, config.AccentColor, name)
                            : ImGui.Checkbox(name, ref on);
                        if (toggled) {
                            selected = on ? selected | bit : selected & ~bit;
                            changed = true;
                        }
                    }
                    ImGui.EndCombo();
                }
                value = (FadeMode)selected;
                break;
            case Enum option:
                var names = choices ?? (enumNames ??= Enum.GetNames(option.GetType()));
                var index = Convert.ToInt32(option);
                changed = config.IsGratis ? ChromeControls.Combo("##value", ref index, names, SettingsTheme.Scale)
                    : ImGui.Combo("##value", ref index, names, names.Length);
                value = Enum.ToObject(option.GetType(), index);
                break;
            case Vector2 vector:
                if (config.IsGratis) {
                    var width = ImGui.CalcItemWidth();
                    ImGui.SetNextItemWidth(width);
                    changed = SettingsTheme.Slider("##x", ref vector.X, minimum, maximum, format, config.AccentColor);
                    ImGui.SetNextItemWidth(width);
                    changed |= SettingsTheme.Slider("##y", ref vector.Y, minimum, maximum, format, config.AccentColor);
                }
                else
                    changed = ImGui.SliderFloat2("##value", ref vector, minimum, maximum, format);
                value = vector;
                break;
            case Vector3 vector:
                changed = ImGui.ColorEdit3("##value", ref vector, ImGuiColorEditFlags.NoInputs);
                value = vector;
                break;
            case Vector4 vector:
                changed = ImGui.ColorEdit4("##value", ref vector, ImGuiColorEditFlags.NoInputs
                    | ImGuiColorEditFlags.AlphaBar);
                value = vector;
                break;
            case string text:
                changed = ImGui.InputText("##value", ref text, 64);
                value = text;
                break;
            default:
                throw new InvalidOperationException($"Unsupported setting type at {Path}.");
        }
        if (changed)
            Set(config, value);
        return changed;
    }
}