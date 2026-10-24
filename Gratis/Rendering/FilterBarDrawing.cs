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
    private static readonly string[] CountLabels = BuildCounts();
    private static readonly string[] Ids = BuildIds();
    private static MapDrawContext? drawnContext;
    public static Vector4 LastBounds { get; private set; }
    public static Vector4 OverflowBounds { get; private set; }
    public static Vector4[] ChipBounds { get; } = new Vector4[MarkerVisibility.GroupCount];
    public static bool Hovered { get; private set; }
    public static bool InputActive { get; private set; }
    public static bool PointerOver => ReferenceEquals(drawnContext, MapDrawContext.Current) &&
        ImGui.IsMouseHoveringRect(new Vector2(LastBounds.X, LastBounds.Y), new Vector2(LastBounds.Z, LastBounds.W), false);
    private static string[] BuildCounts() { var result = new string[1000]; for (var i = 0; i < result.Length; i++) result[i] = i.ToString(global::System.Globalization.CultureInfo.InvariantCulture); return result; }
    private static string[] BuildIds() { var result = new string[MarkerVisibility.GroupCount]; for (var i = 0; i < result.Length; i++) result[i] = "##filter-" + i; return result; }
    public static void Draw()
    {
        var context = MapDrawContext.Current;
        drawnContext = context; LensDrawing.BeginDraw();
        LastBounds = OverflowBounds = Vector4.Zero; Hovered = false; Array.Clear(ChipBounds);
        InputActive = false;
        if (!context.Config.IsGratis) return;
        using var alpha = new ChromeVisibility.AlphaScope(true, context.Chrome.ControlsAlpha(context.Config));
        if (!alpha.Visible) return;
        var scale = context.GlobalScale;
        using var font = context.GetChromeFont(12 * scale, ChromeFontRole.Chip)?.Push();
        var (min, max) = Classes.MapShape.Bounds(Vector2.Zero, ImGui.GetWindowSize());
        var available = (max.X - min.X) * (context.Config.MapCircle ? .72f : 1) - 20 * scale;
        var compact = available < 430 * scale;
        var lensWidth = compact ? 30 * scale : MathF.Min(160 * scale, ImGui.CalcTextSize(context.Visibility.EffectiveLens).X + 44 * scale);
        var count = compact ? 0 : Math.Clamp((int)((available - lensWidth - 38 * scale) / (50 * scale)), 0, MarkerVisibility.GroupCount);
        if (count < MarkerVisibility.GroupCount && available >= lensWidth + MarkerVisibility.GroupCount * 50 * scale) count = MarkerVisibility.GroupCount;
        var overflow = count < MarkerVisibility.GroupCount;
        var size = new Vector2(lensWidth + count * 50 * scale + (overflow ? 34 * scale : 0), 28 * scale);
        var pos = LensDrawing.Position(size.X, size.Y);
        if (available < size.X || pos.Y < ImGui.GetWindowPos().Y + 88 * scale) { LensDrawing.Draw(); return; }
        LastBounds = new(pos.X, pos.Y, pos.X + size.X, pos.Y + size.Y);
        LensDrawing.DrawChip(pos, new Vector2(lensWidth, size.Y), compact);
        Hovered |= ImGui.IsItemHovered();
        var x = pos.X + lensWidth + 4 * scale;
        for (var i = 0; i < count; i++) {
            DrawChip((MarkerGroup)i, new Vector2(x, pos.Y), new Vector2(46 * scale, size.Y));
            x += 50 * scale;
        }
        if (overflow) {
            var start = new Vector2(x, pos.Y);
            var buttonSize = new Vector2(30 * scale, size.Y);
            OverflowBounds = new(start.X, start.Y, start.X + buttonSize.X, start.Y + buttonSize.Y);
            ImGui.SetCursorScreenPos(start);
            if (ImGui.InvisibleButton("##filter-overflow", buttonSize)) ImGui.OpenPopup("Gratis_Filter_Overflow");
            Hovered |= ImGui.IsItemHovered();
            MapWindowDrawing.DrawSurface(ImGui.GetWindowDrawList(), start, start + buttonSize, 8 * scale);
            GratisIcons.Draw(ImGui.GetWindowDrawList(), start + buttonSize / 2, GratisIcons.LayoutGrid, GratisTheme.Primary, 14 * scale);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Marker filters · click to expand");
        }
        LensDrawing.DrawPopup();
        InputActive = Hovered || LensDrawing.InputActive;
        if (!ImGui.BeginPopup("Gratis_Filter_Overflow")) return;
        InputActive = true; context.Chrome.Reveal(ImGui.GetTime());
        ImGui.TextUnformatted("Marker filters");
        ImGui.TextDisabled("Click: mute · Ctrl/Alt: solo · Right-click: loud/quiet");
        ImGui.Separator();
        for (var i = 0; i < MarkerVisibility.GroupCount; i++) {
            var start = ImGui.GetCursorScreenPos();
            DrawChip((MarkerGroup)i, start, new Vector2(310 * scale, 28 * scale), true);
        }
        if (ImGui.Button("Reset filters to lens")) context.Visibility.ResetFilters();
        ImGui.TextDisabled($"Hold {context.Config.PeekKey} over the map to peek.");
        ImGui.EndPopup();
    }
    private static void DrawChip(MarkerGroup group, Vector2 pos, Vector2 size, bool label = false)
    {
        var context = MapDrawContext.Current;
        var model = context.Visibility;
        var index = (int)group;
        var scale = context.GlobalScale;
        var count = model.Counts[index];
        var state = model.State(group, count > 0 && model.PriorityCounts[index] == count);
        ChipBounds[index] = new(pos.X, pos.Y, pos.X + size.X, pos.Y + size.Y);
        ImGui.SetCursorScreenPos(pos);
        ImGui.InvisibleButton(Ids[index], size, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
        var hovered = ImGui.IsItemHovered(); Hovered |= hovered;
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) model.Emphasize(group);
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
            if (ImGui.GetIO().KeyAlt || ImGui.GetIO().KeyCtrl) model.Solo(group);
            else model.Toggle(group);
        }
        var list = ImGui.GetWindowDrawList();
        MapWindowDrawing.DrawSurface(list, pos, pos + size, 8 * scale);
        if (hovered || state == MarkerVisibilityState.Loud)
            list.AddRectFilled(pos + new Vector2(scale), pos + size - new Vector2(scale), ImGui.GetColorU32(context.Config.AccentColor with { W = hovered ? .16f : .1f }), 7 * scale);
        var ink = count == 0 ? GratisTheme.Secondary with { W = .42f } : state == MarkerVisibilityState.Hidden
            ? GratisTheme.Secondary with { W = .58f } : state == MarkerVisibilityState.Loud ? context.Config.AccentColor : GratisTheme.Primary;
        if (state == MarkerVisibilityState.Quiet && count > 0) ink.W = .65f;
        GratisIcons.Draw(list, pos + new Vector2(13 * scale, size.Y / 2), MarkerVisibility.Icons[index], ink, 13 * scale);
        var text = count >= CountLabels.Length ? "999+" : CountLabels[Math.Max(0, count)];
        if (label) {
            list.AddText(pos + new Vector2(28, 7) * scale, ImGui.GetColorU32(ink), MarkerVisibility.Names[index]);
            list.AddText(pos + new Vector2(size.X - 32 * scale, 7 * scale), ImGui.GetColorU32(ink), text);
        }
        else list.AddText(pos + new Vector2(24, 7) * scale, ImGui.GetColorU32(ink), count > 99 ? "99+" : text);
        if (state == MarkerVisibilityState.Hidden) list.AddLine(pos + new Vector2(7, 21) * scale, pos + new Vector2(19, 8) * scale, ImGui.GetColorU32(ink), scale);
        if (hovered) ImGui.SetTooltip($"{MarkerVisibility.Names[index]} · {count} · {state}\nClick: Normal / Hidden (explicit category mute)\nAlt/Ctrl-click: solo; all others Quiet\nRight-click: Loud / Quiet\nPriority markers survive lenses and solo; a category mute hides them.");
    }
}
