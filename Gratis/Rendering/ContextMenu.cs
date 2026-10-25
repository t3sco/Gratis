using System;
using System.Collections.Generic;
using System.Numerics;
using Gratis.Classes;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class ContextMenu
{
    private const string Popup = "##gratis-glass-context";
    private Vector2 anchor;
    private MarkerInfo? candidate;
    private int candidateFrame = -1, selected;
    private uint map;
    private bool swallowEscape;
    public bool IsOpen { get; private set; }
    public bool CapturesInput { get; private set; }
    public Vector2 Min { get; private set; }
    public Vector2 Max { get; private set; }
    public Dictionary<string, Vector2> Rows { get; } = [];
    public int Selected => selected;
    public void Prepare(MapDrawingConfig config)
    {
        swallowEscape &= ImGui.IsKeyDown(ImGuiKey.Escape);
        CapturesInput = (IsOpen || swallowEscape) && config.IsGratis && config.GratisContextMenu;
        if (CapturesInput) Capture();
        else if (!config.IsGratis || !config.GratisContextMenu) IsOpen = false;
    }
    public void Observe(MarkerInfo marker)
    {
        if (!IsOpen && ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right)) {
            candidate = marker; candidateFrame = ImGui.GetFrameCount();
        }
    }
    public void Open(IMapWindowHost host, Vector2 screen, Vector2 origin, MarkerInfo? marker = null)
    {
        anchor = screen; Marker = marker; map = host.Renderer.ViewedMapId;
        World = host.Renderer.ScreenToWorld(marker == null ? screen :
            marker.Position + marker.Offset + ImGui.GetWindowPos(), origin);
        Build(host); IsOpen = CapturesInput = true; ImGui.OpenPopup(Popup); Capture();
    }
    private void Capture()
    {
        ImGui.SetNextFrameWantCaptureKeyboard(true); ImGui.SetNextFrameWantCaptureMouse(true);
        Actions?.CaptureKeys();
    }
    private static bool ImGuiShift() => ImGui.GetIO().KeyShift;
    public void Copy(string text)
    {
        ImGui.SetClipboardText(text); Notify("Copied " + text);
    }
    private string toast = "";
    private double toastUntil;
    public string Toast => toast;
    public void Notify(string message) { toast = message; toastUntil = ImGui.GetTime() + 3; }
    public void Draw(IMapWindowHost host, Vector2 origin)
    {
        Rows.Clear();
        if (!host.Config.IsGratis) return;
        if (host.Config.GratisContextMenu && !IsOpen && !LensDrawing.InputActive &&
            !(host.Config.QuickFilterBar && FilterBarDrawing.InputActive) && !MapDrawContext.Current.WindowMoveInputBlocked &&
            ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem) &&
            MapShape.Contains(ImGui.GetMousePos()) && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            Open(host, ImGui.GetMousePos(), origin, candidateFrame == ImGui.GetFrameCount() ? candidate : null);
        if (IsOpen && map != host.Renderer.ViewedMapId) { IsOpen = false; current = []; }
        if (current.Count > 0) DrawPopup(host);
        DrawToast();
    }
    private void DrawPopup(IMapWindowHost host)
    {
        var context = MapDrawContext.Current; var s = GratisTheme.ChromeScale;
        var (min, max) = MapShape.Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        if (host.Config.MapCircle) { var inset = (max - min) * .14644661f; min += inset; max -= inset; }
        min += new Vector2(GratisTheme.Inset * s); max -= new Vector2(GratisTheme.Inset * s);
        var rowHeight = GratisTheme.ControlHeight * s;
        var footerHeight = max.Y - min.Y >= 3 * rowHeight ? 36 * s : 0;
        var size = Vector2.Min(new Vector2(306 * s, current.Count * rowHeight + 12 * s + footerHeight), Vector2.Max(new Vector2(1), max - min));
        Min = Vector2.Clamp(anchor, min, Vector2.Max(min, max - size)); Max = Min + size;
        ImGui.SetNextWindowPos(Min); ImGui.SetNextWindowSize(size);
        using var font = context.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(6 * s));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Vector4.Zero);
        try {
            using var popup = UiMotion.Popup(Popup, IsOpen && !ImGui.IsKeyPressed(ImGuiKey.Escape, false), Min, size,
                ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings);
            if (!popup.Visible) { IsOpen = false; return; }
            IsOpen = CapturesInput = popup.Interactive;
            if (popup.Interactive) Capture(); context.Chrome.Reveal(host.Now);
            MapWindowDrawing.DrawSurface(ImGui.GetWindowDrawList(), Min, Max, GratisTheme.CardRadius * s, surfaceOpacity: 1);
            if (current.Count == 0) { if (popup.Interactive) Close(); return; }
            if (popup.Interactive && ImGui.IsKeyPressed(ImGuiKey.DownArrow)) selected = (selected + 1) % current.Count;
            if (popup.Interactive && ImGui.IsKeyPressed(ImGuiKey.UpArrow)) selected = (selected + current.Count - 1) % current.Count;
            var enter = popup.Interactive && ImGui.IsKeyPressed(ImGuiKey.Enter, false);
            var navigating = popup.Interactive && (ImGui.IsKeyPressed(ImGuiKey.UpArrow) || ImGui.IsKeyPressed(ImGuiKey.DownArrow));
            UiMotion.Child("##context-actions", new Vector2(0, MathF.Max(1, size.Y - 12 * s - footerHeight)), ImGuiWindowFlags.NoScrollWithMouse | (popup.Interactive ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoInputs));
            UiMotion.Scroll();
            using var rowsMotion = UiMotion.Surface("##context-rows-motion", Min, popup.Interactive);
            for (var i = 0; i < current.Count; i++) {
                var item = current[i]; var pos = ImGui.GetCursorScreenPos();
                var width = MathF.Max(1, ImGui.GetContentRegionAvail().X);
                var enabled = item.Invoke != null || item.Children != null;
                ImGui.BeginDisabled(!enabled);
                var clicked = ChromeControls.Button("##context-row-" + i, new Vector2(width, rowHeight));
                ImGui.EndDisabled();
                Rows[item.Label] = pos + new Vector2(width / 2, rowHeight / 2);
                var hovered = ImGui.IsItemHovered();
                if (hovered && ImGui.GetIO().MouseDelta != Vector2.Zero) selected = i;
                if (navigating && i == selected) UiMotion.ScrollToItem();
                var list = ImGui.GetWindowDrawList();
                using var control = UiMotion.Control(list, pos, pos + new Vector2(width, rowHeight));
                if (i == selected && enabled) {
                    var highlight = UiMotion.Selection("##context-highlight", pos);
                    list.AddRectFilled(highlight, highlight + new Vector2(width, rowHeight), ImGui.GetColorU32(GratisTheme.WarmAccent with { W = .08f }), GratisTheme.ButtonRadius * s);
                }
                ChromeControls.Feedback(list, pos, pos + new Vector2(width, rowHeight), i == selected && enabled);
                GratisIcons.Draw(list, pos + new Vector2(18 * s, rowHeight / 2), item.Icon, GratisTheme.Secondary, GratisTheme.IconSize * s);
                var keycap = i == selected && enabled && item.Children == null && width > 220 * s ? ChromeControls.KeycapSize("Enter") : Vector2.Zero;
                var reserve = item.Children != null ? 24 * s : keycap.X > 0 ? keycap.X + 16 * s : 8 * s;
                var displayed = ChromeControls.FitLabel(item.Label, MathF.Max(0, width - 40 * s - reserve));
                list.AddText(pos + new Vector2(40 * s, (rowHeight - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(enabled ? GratisTheme.Primary : GratisTheme.Secondary), displayed);
                if (keycap.X > 0) ChromeControls.DrawKeycap(list, pos + new Vector2(width - keycap.X - 8 * s, (rowHeight - keycap.Y) / 2), "Enter");
                if (item.Children != null) {
                    var center = pos + new Vector2(width - 16 * s, rowHeight / 2);
                    list.AddLine(center + new Vector2(-2, -4) * s, center + new Vector2(2, 0) * s, ImGui.GetColorU32(GratisTheme.Secondary), s);
                    list.AddLine(center + new Vector2(2, 0) * s, center + new Vector2(-2, 4) * s, ImGui.GetColorU32(GratisTheme.Secondary), s);
                }
                if (displayed != item.Label) ChromeControls.Tooltip(item.Label);
                if (enter && selected == i || clicked) { Activate(item); break; }
            }
            ImGui.EndChild();
            if (footerHeight > 0) {
                ImGui.Separator();
                ChromeControls.Hints(("Up/Down", ""), ("Enter", "Select"), ("Esc", "Close"));
            }
        }
        finally { ImGui.PopStyleColor(); ImGui.PopStyleVar(3); }
        if (details.Length > 0) {
            UiMotion.TooltipText(details);
        }
    }
    private void Activate(ContextMenuItem item)
    {
        if (item.Children is { } children) {
            var sub = new List<ContextMenuItem> { new("Back", GratisIcons.ArrowUp, () => { current = items; selected = 0; }) };
            sub.AddRange(children); current = sub; selected = 0; return;
        }
        if (item.Invoke == null) return;
        LastAction = item.Label; item.Invoke();
        if (item.Label is not ("Show hover details" or "Back")) Close();
    }
    private void Close() { swallowEscape = ImGui.IsKeyDown(ImGuiKey.Escape); IsOpen = false; ImGui.CloseCurrentPopup(); }
    private void DrawToast()
    {
        var alpha = UiMotion.Visibility(ImGui.GetID("##toast-motion"), toast.Length > 0 && ImGui.GetTime() < toastUntil);
        if (alpha <= .001f) return;
        var context = MapDrawContext.Current; var s = GratisTheme.ChromeScale;
        using var font = context.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Chip)?.Push();
        var size = ImGui.CalcTextSize(toast) + new Vector2(24, 16) * s;
        var pos = MapAssistance.ConstrainPanel(ImGui.GetWindowPos() + new Vector2((ImGui.GetWindowWidth() - size.X) / 2, ImGui.GetWindowHeight() - 112 * s), size);
        using var motion = new UiMotion.VisualScope(ImGui.GetWindowDrawList(), pos, alpha, UiMotion.Full ? new Vector2(0, (1 - alpha) * 6 * s) : Vector2.Zero);
        MapWindowDrawing.DrawSurface(ImGui.GetWindowDrawList(), pos, pos + size, 8 * s, surfaceOpacity: 1);
        ImGui.GetWindowDrawList().AddText(pos + new Vector2(12, 8) * s, ImGui.GetColorU32(GratisTheme.Primary), toast);
    }
}
