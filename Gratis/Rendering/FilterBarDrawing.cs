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
    public static Vector4 LastBounds { get; private set; }
    public static Vector4 OverflowBounds { get; private set; }
    public static Vector4 SettingsBounds { get; private set; }
    public static Vector4[] ChipBounds { get; } = new Vector4[MarkerVisibility.GroupCount];
    public static bool Hovered { get; private set; }
    public static bool InputActive { get; private set; }
    public static bool PopoverOpen { get; private set; }
    public static int ActiveCount { get; private set; }
    public static string LastTooltip { get; private set; } = "";
    public static bool PointerOver => ReferenceEquals(drawnContext, MapDrawContext.Current) &&
        ImGui.IsMouseHoveringRect(new(LastBounds.X, LastBounds.Y), new(LastBounds.Z, LastBounds.W), false);
    private static string[] BuildIds() { var result = new string[MarkerVisibility.GroupCount]; for (var i = 0; i < result.Length; i++) result[i] = "##filter-" + i; return result; }
    public static string TooltipLabel(MarkerGroup group, MarkerVisibilityState state) => Names[(int)group] + (state switch {
        MarkerVisibilityState.Hidden => " \u00b7 Muted", MarkerVisibilityState.Loud => " \u00b7 Loud",
        MarkerVisibilityState.Quiet => " \u00b7 Quiet", _ => "",
    });
    private static void Tooltip(string text) { LastTooltip = text; ImGui.SetTooltip(text); }

    public static void Draw()
    {
        var context = MapDrawContext.Current;
        if (!ReferenceEquals(drawnContext, context)) { popupFrame = -2; suppressOpen = false; }
        drawnContext = context; LensDrawing.BeginDraw();
        LastBounds = OverflowBounds = SettingsBounds = Vector4.Zero; Array.Clear(ChipBounds);
        Hovered = InputActive = PopoverOpen = false; LastTooltip = ""; ActiveCount = 0;
        if (!context.Config.IsGratis) return;
        using var alpha = new ChromeVisibility.AlphaScope(true, context.Chrome.ControlsAlpha(context.Config));
        if (!alpha.Visible) return;
        var options = context.Config.FilterOptions;
        var s = GratisTheme.ChromeScale;
        using var font = context.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Chip)?.Push();
        var button = GratisTheme.ControlHeight * s; var gap = 4 * s; var stride = button + gap;
        var available = ChromeLayout.AvailableWidth(context.Config.FilterBarAnchor);
        var slots = options.Mode == FilterBarMode.AlwaysExpanded ? Math.Max(0, (int)((available - 2 * stride) / stride)) : 0;
        var visible = 0;
        for (var i = 0; i < MarkerVisibility.GroupCount; i++) {
            if (context.Visibility.Overrides[i] is { } state && state != MarkerVisibilityState.Normal) ActiveCount++;
            if (options.Contains((MarkerGroup)i)) visible++;
        }
        var count = Math.Min(slots, visible);
        var expanded = options.Mode == FilterBarMode.AlwaysExpanded;
        var size = new Vector2(expanded ? (count + 2) * stride - gap : button, button);
        var pos = LensDrawing.Position(size.X, size.Y);
        if (pos.Y == float.MinValue) return;
        LastBounds = new(pos.X, pos.Y, pos.X + size.X, pos.Y + size.Y);
        OverflowBounds = new(pos.X, pos.Y, pos.X + button, pos.Y + button);
        var onButton = ImGui.IsMouseHoveringRect(pos, pos + new Vector2(button), false);
        var close = popupFrame >= ImGui.GetFrameCount() - 1 && onButton && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        if (close) suppressOpen = true;
        ImGui.SetCursorScreenPos(pos);
        var clicked = ImGui.InvisibleButton("##filter-overflow", new Vector2(button));
        Hovered |= ImGui.IsItemHovered();
        if (clicked && !suppressOpen) ImGui.OpenPopup(Popup);
        MapWindowDrawing.DrawSurface(ImGui.GetWindowDrawList(), pos, pos + new Vector2(button), GratisTheme.CardRadius * s);
        GratisIcons.Draw(ImGui.GetWindowDrawList(), pos + new Vector2(button / 2), GratisIcons.LayoutGrid, GratisTheme.Primary, GratisTheme.IconSize * s);
        if (ActiveCount > 0) {
            using var badgeFont = context.GetChromeFont(10 * s, ChromeFontRole.Chip)?.Push();
            var center = pos + new Vector2(button - 6 * s, 6 * s);
            var label = ActiveCount.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
            var labelSize = ImGui.CalcTextSize(label);
            ImGui.GetWindowDrawList().AddCircleFilled(center, 8 * s, ImGui.GetColorU32(GratisTheme.WarmAccent), 20);
            ImGui.GetWindowDrawList().AddText(center - labelSize / 2, ImGui.GetColorU32(GratisTheme.Surface with { W = 1 }), label);
        }
        if (ImGui.IsItemHovered()) Tooltip(ActiveCount > 0 ? $"Filters \u00b7 {ActiveCount} active" : "Filters");
        var x = pos.X + stride; var submitted = 0;
        if (expanded) {
            for (var i = 0; i < MarkerVisibility.GroupCount && submitted < count; i++) {
                if (!options.Contains((MarkerGroup)i)) continue;
                DrawChip((MarkerGroup)i, new(x, pos.Y), new(button)); x += stride; submitted++;
            }
            DrawSettings(new(x, pos.Y), new(button));
        }
        InputActive = Hovered;
        DrawPopover(pos, button, stride, visible, close);
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) suppressOpen = false;
    }

    private static void DrawSettings(Vector2 pos, Vector2 size)
    {
        SettingsBounds = new(pos.X, pos.Y, pos.X + size.X, pos.Y + size.Y);
        ImGui.SetCursorScreenPos(pos);
        var clicked = ImGui.InvisibleButton("##filter-settings", size);
        Hovered |= ImGui.IsItemHovered();
        MapWindowDrawing.DrawSurface(ImGui.GetWindowDrawList(), pos, pos + size, GratisTheme.CardRadius * GratisTheme.ChromeScale);
        GratisIcons.Draw(ImGui.GetWindowDrawList(), pos + size / 2, GratisIcons.Settings, GratisTheme.Secondary, GratisTheme.IconSize * GratisTheme.ChromeScale);
        if (ImGui.IsItemHovered()) Tooltip("Filter settings");
        if (!clicked) return;
        if (PopoverOpen) ImGui.CloseCurrentPopup();
        MapDrawContext.Current.OpenFilterSettings();
    }

    private static void DrawPopover(Vector2 anchor, float button, float stride, int visible, bool close)
    {
        var context = MapDrawContext.Current; var s = GratisTheme.ChromeScale;
        var viewport = ImGui.GetWindowViewport(); var min = viewport.Pos + new Vector2(GratisTheme.Inset * s);
        var max = viewport.Pos + viewport.Size - new Vector2(GratisTheme.Inset * s);
        var budget = Math.Clamp((int)((max.X - min.X - 20 * s) / stride), 1, 6);
        var columns = Math.Clamp(visible, 1, budget); var rows = (visible + columns - 1) / columns;
        var size = new Vector2(columns * stride + 20 * s, MathF.Min(rows * stride + 64 * s, max.Y - min.Y));
        var top = context.Config.FilterBarAnchor < ChromeAnchor.BottomLeft ? anchor.Y + button + 8 * s : anchor.Y - size.Y - 8 * s;
        ImGui.SetNextWindowPos(Vector2.Clamp(new(anchor.X, top), min, Vector2.Max(min, max - size)));
        ImGui.SetNextWindowSize(size);
        using var style = new ChromePopupStyle();
        if (!ImGui.BeginPopup(Popup, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings)) return;
        if (close) { ImGui.CloseCurrentPopup(); ImGui.EndPopup(); return; }
        PopoverOpen = InputActive = true; popupFrame = ImGui.GetFrameCount(); context.Chrome.Reveal(ImGui.GetTime());
        var origin = ImGui.GetCursorScreenPos(); var index = 0;
        for (var i = 0; i < MarkerVisibility.GroupCount; i++) {
            if (!context.Config.FilterOptions.Contains((MarkerGroup)i)) continue;
            DrawChip((MarkerGroup)i, origin + new Vector2(index % columns * stride, index / columns * stride), new(button)); index++;
        }
        DrawSettings(origin + new Vector2((columns - 1) * stride, rows * stride + 4 * s), new(button));
        ImGui.EndPopup();
    }

    private static void DrawChip(MarkerGroup group, Vector2 pos, Vector2 size)
    {
        var context = MapDrawContext.Current; var model = context.Visibility; var index = (int)group;
        var s = GratisTheme.ChromeScale; var count = model.Counts[index];
        var state = model.State(group, count > 0 && model.PriorityCounts[index] == count);
        ChipBounds[index] = new(pos.X, pos.Y, pos.X + size.X, pos.Y + size.Y);
        ImGui.SetCursorScreenPos(pos);
        ImGui.InvisibleButton(Ids[index], size, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
        var hovered = ImGui.IsItemHovered(); Hovered |= hovered;
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) model.Emphasize(group);
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
            if (ImGui.GetIO().KeyAlt || ImGui.GetIO().KeyCtrl) model.Solo(group); else model.Toggle(group);
        }
        var list = ImGui.GetWindowDrawList();
        MapWindowDrawing.DrawSurface(list, pos, pos + size, GratisTheme.CardRadius * s);
        if (hovered || state == MarkerVisibilityState.Loud)
            list.AddRectFilled(pos + new Vector2(s), pos + size - new Vector2(s), ImGui.GetColorU32(new Vector4(1, 1, 1, hovered ? .1f : .06f)), GratisTheme.ButtonRadius * s);
        var ink = GratisTheme.FilterTints[index];
        ink.W = state == MarkerVisibilityState.Hidden ? .5f : state == MarkerVisibilityState.Quiet || count == 0 ? .75f : 1;
        var glyph = group == MarkerGroup.Other ? GratisIcons.Info : MarkerVisibility.Icons[index];
        GratisIcons.Draw(list, pos + size / 2, glyph, ink, GratisTheme.IconSize * s);
        if (state == MarkerVisibilityState.Hidden)
            list.AddLine(pos + size * new Vector2(.25f, .75f), pos + size * new Vector2(.75f, .25f), ImGui.GetColorU32(GratisTheme.Primary), s);
        if (state == MarkerVisibilityState.Loud)
            list.AddLine(pos + new Vector2(8 * s, size.Y - s), pos + new Vector2(size.X - 8 * s, size.Y - s), ImGui.GetColorU32(GratisTheme.WarmAccent), s);
        if (hovered) Tooltip(TooltipLabel(group, state));
    }
}
