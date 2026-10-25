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
    private static string[] lensNames = [];
    private static readonly string[] StateNames = ["Loud", "Normal", "Quiet", "Hidden"];
    private static object? lensOwner;
    private static MapDrawContext? drawnContext;
    public static Vector4 LastBounds { get; private set; }
    public static bool PointerOver => ReferenceEquals(drawnContext, MapDrawContext.Current) &&
        ImGui.IsMouseHoveringRect(new Vector2(LastBounds.X, LastBounds.Y), new Vector2(LastBounds.Z, LastBounds.W), false);
    public static bool InputActive { get; private set; }
    public static void BeginDraw() { LastBounds = Vector4.Zero; InputActive = false; }
    public static Vector2 Position(float width, float height)
    {
        var bounds = ChromeLayout.Place("filters", new Vector2(width, height), MapDrawContext.Current.Config.FilterBarAnchor);
        return bounds == Vector4.Zero ? new Vector2(float.MinValue) : ChromeLayout.Position(bounds);
    }
    public static void Draw()
    {
        BeginDraw();
        var context = MapDrawContext.Current;
        if (!context.Config.IsGratis) return;
        using var alpha = new ChromeVisibility.AlphaScope(true, context.Chrome.ControlsAlpha(context.Config));
        LastBounds = Vector4.Zero;
        if (!alpha.Visible) return;
        using var font = context.GetChromeFont(GratisTheme.TextSize * GratisTheme.ChromeScale, ChromeFontRole.Chip)?.Push();
        var size = new Vector2(MathF.Min(160 * GratisTheme.ChromeScale, ImGui.CalcTextSize(context.Visibility.EffectiveLens).X + 44 * GratisTheme.ChromeScale), GratisTheme.ControlHeight * GratisTheme.ChromeScale);
        var pos = Position(size.X, size.Y);
        if (pos.Y == float.MinValue) return;
        DrawChip(pos, size);
    }
    public static void DrawChip(Vector2 pos, Vector2 size, bool compact = false)
    {
        drawnContext = MapDrawContext.Current;
        var context = MapDrawContext.Current;
        var list = ImGui.GetWindowDrawList();
        LastBounds = new Vector4(pos.X, pos.Y, pos.X + size.X, pos.Y + size.Y);
        ImGui.SetCursorScreenPos(pos);
        if (ImGui.InvisibleButton("##lens-switcher", size)) context.OpenFilterSettings();
        InputActive |= ImGui.IsItemHovered();
        MapWindowDrawing.DrawSurface(list, pos, pos + size, 8 * GratisTheme.ChromeScale);
        GratisIcons.Draw(list, pos + new Vector2(15 * GratisTheme.ChromeScale, size.Y / 2), GratisIcons.Layers, context.Config.AccentColor, 14 * GratisTheme.ChromeScale);
        if (!compact) list.AddText(pos + new Vector2(29 * GratisTheme.ChromeScale, (size.Y - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(GratisTheme.Primary), context.Visibility.EffectiveLens);
        if (ImGui.IsItemHovered()) UiMotion.TooltipText("Lens: " + context.Visibility.EffectiveLens);
    }
    public static bool DrawSelector(MapDrawingConfig config)
    {
        var count = Lenses.BuiltIn.Length + (config.CustomLenses?.Count ?? 0);
        if (lensOwner != config.CustomLenses || lensNames.Length != count || Array.IndexOf(lensNames, config.ActiveLens) < 0) {
            lensOwner = config.CustomLenses; lensNames = new string[count];
            for (var i = 0; i < Lenses.BuiltIn.Length; i++) lensNames[i] = Lenses.BuiltIn[i].Name;
            for (var i = Lenses.BuiltIn.Length; i < count; i++) lensNames[i] = config.CustomLenses![i - Lenses.BuiltIn.Length]?.Name ?? "Unnamed lens";
        }
        var selected = Math.Max(0, Array.IndexOf(lensNames, config.ActiveLens));
        if (!ChromeControls.Combo("##value", ref selected, lensNames)) return false;
        config.ActiveLens = lensNames[selected]; return true;
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
                if (ChromeControls.Combo("##state", ref state, StateNames)) {
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
