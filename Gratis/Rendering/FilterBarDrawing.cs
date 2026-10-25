using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class FilterBarDrawing
{
    private const string Popup = "Gratis_Filter_Overflow";
    private static readonly string[] Names = ["Quests", "Side quests", "FATEs", "Aetherytes", "Exits", "Services", "Housing", "Gathering / hunts", "Treasure", "Party", "Other", "Pins"];
    private static readonly string[] Ids = BuildIds();
    private static MapDrawContext? drawnContext;
    private static int popupFrame = -2;
    private static bool suppressOpen;
    private static MapDrawContext? toggleContext;
    public static void TogglePopover() => toggleContext = MapDrawContext.Current;
    public static Vector4 LastBounds { get; private set; }
    public static Vector4 OverflowBounds { get; private set; }
    public static Vector4 SettingsBounds { get; private set; }
    public static Vector4 DeclutterBounds { get; private set; }
    public static Vector4 PopoverBounds { get; private set; }
    public static Vector4[] ChipBounds { get; } = new Vector4[MarkerVisibility.GroupCount];
    public static bool Hovered { get; private set; }
    public static bool InputActive { get; private set; }
    public static bool PopoverOpen { get; private set; }
    public static int ActiveCount { get; private set; }
    public static int MutedCount { get; private set; }
    public static string TriggerLabel { get; private set; } = "";
    public static int SoloGroup { get; private set; } = -1;
    public static string LastTooltip { get; private set; } = "";
    public static bool PointerOver => ReferenceEquals(drawnContext, MapDrawContext.Current) &&
        ImGui.IsMouseHoveringRect(new(LastBounds.X, LastBounds.Y), new(LastBounds.Z, LastBounds.W), false);
    private static string[] BuildIds() { var result = new string[MarkerVisibility.GroupCount]; for (var i = 0; i < result.Length; i++) result[i] = "##filter-" + i; return result; }
    public static string TooltipLabel(MarkerGroup group, MarkerVisibilityState state) => Names[(int)group] + (state switch {
        MarkerVisibilityState.Hidden => " \u00b7 Muted", MarkerVisibilityState.Loud => " \u00b7 Loud",
        MarkerVisibilityState.Quiet => " \u00b7 Quiet", _ => "",
    });
    private static void Tooltip(string text)
    {
        LastTooltip = text;
        ChromeControls.Tooltip(text, windowBounds: MapToolbarDrawing.WindowBounds);
    }
    private static bool ShowGroup(MarkerGroup group) => MapDrawContext.Current.Config.FilterOptions.Contains(group) &&
        (MapDrawContext.Current.Visibility.Counts[(int)group] > 0 || MapDrawContext.Current.Visibility.Overrides[(int)group] == MarkerVisibilityState.Hidden);

    public static void Draw()
    {
        var context = MapDrawContext.Current;
        if (!ReferenceEquals(drawnContext, context)) { popupFrame = -2; suppressOpen = false; }
        drawnContext = context; LensDrawing.BeginDraw();
        LastBounds = OverflowBounds = SettingsBounds = DeclutterBounds = PopoverBounds = Vector4.Zero; Array.Clear(ChipBounds);
        Hovered = InputActive = PopoverOpen = false; LastTooltip = TriggerLabel = ""; ActiveCount = MutedCount = 0;
        if (!context.Config.IsGratis) return;
        using var alpha = new ChromeVisibility.AlphaScope(true, context.Chrome.ControlsAlpha(context.Config));
        if (!alpha.Visible) return;
        var s = GratisTheme.ChromeScale;
        using var font = context.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Chip)?.Push();
        var button = GratisTheme.ControlHeight * s; var gap = 4 * s; var stride = button + gap;
        var visible = 0;
        for (var i = 0; i < MarkerVisibility.GroupCount; i++) {
            if (context.Visibility.Overrides[i] is { } state && state != MarkerVisibilityState.Normal) ActiveCount++;
            if (context.Visibility.Overrides[i] == MarkerVisibilityState.Hidden) MutedCount++;
            if (ShowGroup((MarkerGroup)i)) visible++;
        }
        SoloGroup = -1;
        for (var i = 0; i < MarkerVisibility.GroupCount; i++) {
            if (context.Visibility.Overrides[i] != MarkerVisibilityState.Normal) continue;
            var solo = true;
            for (var j = 0; j < MarkerVisibility.GroupCount; j++)
                if (i != j && context.Visibility.Overrides[j] != MarkerVisibilityState.Quiet) solo = false;
            if (solo) SoloGroup = i;
        }
        TriggerLabel = SoloGroup >= 0 ? "Solo" : MutedCount > 0 ? $"{MutedCount} muted" : ActiveCount > 0 ? $"{ActiveCount} active" : "";
        var toolbar = MapToolbarDrawing.ButtonBounds[2];
        if (toolbar == Vector4.Zero) {
            toggleContext = null;
            if (ImGui.BeginPopup(Popup)) { ImGui.CloseCurrentPopup(); ImGui.EndPopup(); }
            return;
        }
        var onButton = ImGui.IsMouseHoveringRect(new(toolbar.X, toolbar.Y), new(toolbar.Z, toolbar.W), false);
        var close = popupFrame >= ImGui.GetFrameCount() - 1 && onButton && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        if (close) suppressOpen = true;
        if (ReferenceEquals(toggleContext, context)) {
            if (popupFrame >= ImGui.GetFrameCount() - 1) close = true; else if (!suppressOpen) ImGui.OpenPopup(Popup);
            toggleContext = null;
        }
        DrawPopover(toolbar, button, stride, visible, close);
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) suppressOpen = false;
    }

    private static void DrawSettings(Vector2 pos, Vector2 size)
    {
        SettingsBounds = new(pos.X, pos.Y, pos.X + size.X, pos.Y + size.Y);
        ImGui.SetCursorScreenPos(pos);
        var clicked = ChromeControls.Button("##filter-settings", size);
        Hovered |= ImGui.IsItemHovered();
        ChromeControls.Feedback(ImGui.GetWindowDrawList(), pos, pos + size);
        GratisIcons.Draw(ImGui.GetWindowDrawList(), pos + size / 2, GratisIcons.Settings, GratisTheme.Secondary, GratisTheme.IconSize * GratisTheme.ChromeScale);
        if (ImGui.IsItemHovered()) Tooltip("Filter settings");
        if (!clicked) return;
        if (PopoverOpen) ImGui.CloseCurrentPopup();
        MapDrawContext.Current.OpenFilterSettings();
    }

    private static void DrawPopover(Vector4 trigger, float button, float stride, int visible, bool close)
    {
        var context = MapDrawContext.Current; var s = GratisTheme.ChromeScale;
        var (min, max) = ChromeLayout.SafeBounds();
        var budget = Math.Clamp((int)((max.X - min.X - 24 * s) / stride), 1, 7);
        var columns = Math.Clamp(visible + 1, 1, budget); var rows = (visible + columns) / columns;
        var size = new Vector2(MathF.Min(MathF.Max(240 * s, columns * stride + 20 * s), max.X - min.X), MathF.Min(rows * stride + 60 * s, max.Y - min.Y));
        var bounds = ChromePopoverMath.Place(trigger, size, min, max, GratisTheme.Gap * s);
        size = new(bounds.Z - bounds.X, bounds.W - bounds.Y);
        ImGui.SetNextWindowPos(new(bounds.X, bounds.Y));
        ImGui.SetNextWindowSize(size);
        using var style = new ChromePopupStyle(transparent: true);
        using var popup = UiMotion.Popup(Popup, !close && !ImGui.IsKeyPressed(ImGuiKey.Escape, false),
            new(bounds.X, bounds.Y), size, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollWithMouse);
        if (!popup.Visible) return;
        UiMotion.Scroll();
        if (popup.Interactive) LastBounds = PopoverBounds = bounds;
        PopoverOpen = InputActive = popup.Interactive; if (popup.Interactive) popupFrame = ImGui.GetFrameCount(); context.Chrome.Reveal(ImGui.GetTime());
        MapWindowDrawing.DrawSurface(ImGui.GetWindowDrawList(), ImGui.GetWindowPos(), ImGui.GetWindowPos() + size, GratisTheme.CardRadius * s);
        var origin = ImGui.GetCursorScreenPos(); var index = 0;
        for (var i = 0; i < MarkerVisibility.GroupCount; i++) {
            if (!ShowGroup((MarkerGroup)i)) continue;
            DrawChip((MarkerGroup)i, origin + new Vector2(index % columns * stride, index / columns * stride), new(button)); index++;
        }
        DrawSettings(origin + new Vector2(index % columns * stride, index / columns * stride), new(button));
        if (visible == 0) {
            using var emptyFont = context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push();
            ImGui.GetWindowDrawList().AddText(origin + new Vector2(stride, (button - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(GratisTheme.Primary), "No markers here yet");
        }
        ImGui.SetCursorScreenPos(origin + new Vector2(0, rows * stride + 4 * s));
        using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push()) {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.72f, .72f, .72f, 1));
            ImGui.TextWrapped("Click mute · Ctrl/Alt solo · Right loud/quiet");
            ImGui.PopStyleColor();
        }
    }

    private static void DrawChip(MarkerGroup group, Vector2 pos, Vector2 size)
    {
        var context = MapDrawContext.Current; var model = context.Visibility; var index = (int)group;
        var s = GratisTheme.ChromeScale; var count = model.Counts[index];
        var state = model.State(group, count > 0 && model.PriorityCounts[index] == count);
        ChipBounds[index] = new(pos.X, pos.Y, pos.X + size.X, pos.Y + size.Y);
        ImGui.SetCursorScreenPos(pos);
        var activated = ChromeControls.Button(Ids[index], size);
        var hovered = ImGui.IsItemHovered(); Hovered |= hovered;
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) model.Emphasize(group);
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Left) || activated && !ImGui.IsMouseReleased(ImGuiMouseButton.Left) && !ImGui.IsMouseReleased(ImGuiMouseButton.Right)) {
            if (ImGui.GetIO().KeyAlt || ImGui.GetIO().KeyCtrl) model.Solo(group); else model.Toggle(group);
        }
        var list = ImGui.GetWindowDrawList();
        using var motion = UiMotion.Control(list, pos, pos + size);
        ChromeControls.Feedback(list, pos, pos + size, state == MarkerVisibilityState.Loud);
        var ink = GratisTheme.FilterTints[index];
        ink = state == MarkerVisibilityState.Hidden ? GratisTheme.Secondary : ink;
        ink.W = state == MarkerVisibilityState.Quiet || count == 0 ? .65f : 1;
        var glyph = group == MarkerGroup.Other ? GratisIcons.Info : MarkerVisibility.Icons[index];
        GratisIcons.Draw(list, pos + size / 2, glyph, ink, GratisTheme.IconSize * s);
        var indicator = pos + size - new Vector2(7 * s);
        list.AddCircleFilled(indicator, 5 * s, ImGui.GetColorU32(GratisTheme.Surface with { W = 1 }), 16);
        var mute = UiMotion.Value(UiMotion.Item, 18, state == MarkerVisibilityState.Hidden ? 1 : 0);
        var solo = UiMotion.Value(UiMotion.Item, 19, index == SoloGroup ? 1 : 0);
        var quiet = UiMotion.Value(UiMotion.Item, 20, state == MarkerVisibilityState.Quiet ? 1 : 0);
        var normal = UiMotion.Value(UiMotion.Item, 21, state != MarkerVisibilityState.Hidden && state != MarkerVisibilityState.Quiet && index != SoloGroup ? 1 : 0);
        if (mute > .001f) list.AddLine(indicator - new Vector2(2.5f * s, 0), indicator + new Vector2(2.5f * s, 0), ImGui.GetColorU32(GratisTheme.Secondary with { W = mute }), s);
        if (solo > .001f) {
            list.AddCircle(indicator, 3 * s, ImGui.GetColorU32(GratisTheme.WarmAccent with { W = solo }), 12, s);
            list.AddCircleFilled(indicator, s, ImGui.GetColorU32(GratisTheme.WarmAccent with { W = solo }), 8);
        }
        if (quiet > .001f) list.AddCircle(indicator, 2.5f * s, ImGui.GetColorU32(GratisTheme.Secondary with { W = quiet }), 12, s);
        if (normal > .001f) {
            list.AddLine(indicator + new Vector2(-2, 0) * s, indicator + new Vector2(0, 2) * s, ImGui.GetColorU32(GratisTheme.WarmAccent with { W = normal }), s);
            list.AddLine(indicator + new Vector2(0, 2) * s, indicator + new Vector2(3, -2) * s, ImGui.GetColorU32(GratisTheme.WarmAccent with { W = normal }), s);
        }
        if (state == MarkerVisibilityState.Loud)
            list.AddLine(pos + new Vector2(8 * s, size.Y - s), pos + new Vector2(size.X - 8 * s, size.Y - s), ImGui.GetColorU32(GratisTheme.WarmAccent), s);
        if (hovered) Tooltip(index == SoloGroup ? Names[index] + " · Solo" : TooltipLabel(group, state));
    }
}
