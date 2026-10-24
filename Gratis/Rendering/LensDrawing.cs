using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class LensDrawing
{
    private static string customName = "My lens";
    private static bool recordingKey;
    public static Vector4 LastBounds { get; private set; }
    public static bool InputActive { get; private set; }
    public static void BeginDraw() { LastBounds = Vector4.Zero; InputActive = false; }
    public static Vector2 Position(float width, float height)
    {
        var context = MapDrawContext.Current;
        var scale = context.GlobalScale;
        var origin = ImGui.GetWindowPos();
        var (min, max) = Classes.MapShape.Bounds(Vector2.Zero, ImGui.GetWindowSize());
        var bottom = max.Y - 10 * scale;
        var coordinates = MapWindowDrawing.CoordinateBounds;
        if (coordinates.W > coordinates.Y) bottom = MathF.Min(bottom, coordinates.Y - origin.Y - 8 * scale);
        var title = MapWindowDrawing.TitleBounds;
        if (title.W > title.Y && title.Y > origin.Y + (max.Y - min.Y) / 2) bottom = MathF.Min(bottom, title.Y - origin.Y - 8 * scale);
        var x = min.X + 10 * scale;
        var y = bottom - height;
        if (context.Config.MapCircle) {
            var radius = (max.X - min.X) / 2;
            x = (min.X + max.X - width) / 2;
            var half = width / 2 + 10 * scale;
            y = MathF.Min(y, max.Y - radius + MathF.Sqrt(MathF.Max(0, radius * radius - half * half)) - height - 10 * scale);
        }
        return origin + new Vector2(x, y);
    }
    public static void Draw()
    {
        BeginDraw();
        var context = MapDrawContext.Current;
        if (!context.Config.IsGratis) return;
        using var alpha = new ChromeVisibility.AlphaScope(true, context.Chrome.ControlsAlpha(context.Config));
        LastBounds = Vector4.Zero;
        if (!alpha.Visible) return;
        using var font = context.GetChromeFont(12 * context.GlobalScale, ChromeFontRole.Chip)?.Push();
        var size = new Vector2(MathF.Min(160 * context.GlobalScale, ImGui.CalcTextSize(context.Visibility.EffectiveLens).X + 44 * context.GlobalScale), 28 * context.GlobalScale);
        var pos = Position(size.X, size.Y);
        if (pos.Y < ImGui.GetWindowPos().Y + 88 * context.GlobalScale) return;
        DrawChip(pos, size);
        DrawPopup();
    }
    public static void DrawChip(Vector2 pos, Vector2 size, bool compact = false)
    {
        var context = MapDrawContext.Current;
        var list = ImGui.GetWindowDrawList();
        LastBounds = new Vector4(pos.X, pos.Y, pos.X + size.X, pos.Y + size.Y);
        ImGui.SetCursorScreenPos(pos);
        if (ImGui.InvisibleButton("##lens-switcher", size)) ImGui.OpenPopup("Gratis_Lenses");
        InputActive |= ImGui.IsItemHovered();
        MapWindowDrawing.DrawSurface(list, pos, pos + size, 8 * context.GlobalScale);
        GratisIcons.Draw(list, pos + new Vector2(15 * context.GlobalScale, size.Y / 2), GratisIcons.Layers, context.Config.AccentColor, 14 * context.GlobalScale);
        if (!compact) list.AddText(pos + new Vector2(29, 7) * context.GlobalScale, ImGui.GetColorU32(GratisTheme.Primary), context.Visibility.EffectiveLens);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Lens presets · priority markers stay visible\nAuto is opt-in. Right Alt: peek (configurable).");
    }
    public static bool DrawSelector(MapDrawingConfig config)
    {
        var changed = false;
        if (!ImGui.BeginCombo("##value", config.ActiveLens)) return false;
        foreach (var lens in Lenses.BuiltIn)
            if (ImGui.Selectable(lens.Name, lens.Name == config.ActiveLens)) { config.ActiveLens = lens.Name; changed = true; }
        if (config.CustomLenses is { } custom)
            foreach (var lens in custom)
                if (lens is not null && ImGui.Selectable(lens.Name, lens.Name == config.ActiveLens)) { config.ActiveLens = lens.Name; changed = true; }
        ImGui.EndCombo();
        return changed;
    }
    public static bool DrawEditor(MapDrawingConfig config)
    {
        var changed = false;
        ImGui.InputTextWithHint("##lens-name", "Custom lens name", ref customName, 40);
        if (ImGui.Button("Save current as custom lens") && !string.IsNullOrWhiteSpace(customName)) {
            var name = customName.Trim();
            var reserved = false;
            foreach (var preset in Lenses.BuiltIn) reserved |= string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase);
            if (!reserved) {
                config.CustomLenses ??= [];
                config.CustomLenses.RemoveAll(lens => lens is null || lens.Name == name);
                config.CustomLenses.Add(MapDrawContext.Current.Visibility.Snapshot(name));
                config.ActiveLens = name; changed = true;
            }
        }
        var selected = Lenses.Find(config, config.ActiveLens);
        if (config.CustomLenses?.Contains(selected) == true) {
            for (var i = 0; i < MarkerVisibility.GroupCount; i++) {
                var state = (int)selected.State((MarkerGroup)i);
                ImGui.PushID(i);
                ImGui.SetNextItemWidth(100 * MapDrawContext.Current.GlobalScale);
                if (ImGui.Combo("##state", ref state, "Loud\0Normal\0Quiet\0Hidden\0")) {
                    if (selected.States is null || selected.States.Length != MarkerVisibility.GroupCount) selected.States = Lens.CreateStates(MarkerVisibilityState.Normal);
                    selected.States[i] = (MarkerVisibilityState)state; changed = true;
                }
                ImGui.SameLine(); ImGui.TextUnformatted(MarkerVisibility.Names[i]); ImGui.PopID();
            }
            if (ImGui.Button("Delete custom lens")) { config.CustomLenses.Remove(selected); config.ActiveLens = "Explore"; changed = true; }
        }
        return changed;
    }
    public static bool DrawKeyBinding(MapDrawingConfig config)
    {
        if (ImGui.Button(recordingKey ? "Press a key (Escape clears)" : config.LensCycleKey == 0 ? "Bind lens cycle key" : $"Cycle: {(ImGuiKey)config.LensCycleKey}")) recordingKey = true;
        if (!recordingKey) return false;
        foreach (var key in Enum.GetValues<ImGuiKey>()) {
            if (key < ImGuiKey.Tab || key > ImGuiKey.F12 || !ImGui.IsKeyPressed(key, false)) continue;
            config.LensCycleKey = key == ImGuiKey.Escape ? 0 : (int)key;
            recordingKey = false; return true;
        }
        return false;
    }
    public static void DrawPopup()
    {
        if (!ImGui.BeginPopup("Gratis_Lenses")) return;
        InputActive = true;
        var context = MapDrawContext.Current;
        var config = context.Config;
        var changed = DrawSelector(config);
        changed |= ImGui.Checkbox("Auto (duty / gathering context)", ref config.LensAuto);
        changed |= ImGui.Checkbox("Lens map tint", ref config.LensTintsMap);
        changed |= DrawKeyBinding(config);
        ImGui.Separator();
        changed |= DrawEditor(config);
        if (changed) context.SaveConfig();
        ImGui.EndPopup();
    }
    public static void DrawMapHint()
    {
        var context = MapDrawContext.Current;
        if (!context.Config.IsGratis || !context.Config.LensTintsMap) return;
        var tint = Lenses.Find(context.Config, context.Visibility.EffectiveLens).MapTint;
        if (tint.W <= 0) return;
        var (min, max) = Classes.MapShape.Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        var inset = Classes.MapShape.FeatherWidth;
        ImGui.GetWindowDrawList().AddRectFilled(min + new Vector2(inset), max - new Vector2(inset), ImGui.GetColorU32(tint), MathF.Max(0, Classes.MapShape.Current - inset));
    }
}
