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
        if (IsOpen) DrawPopup(host);
        DrawToast();
    }
    private void DrawPopup(IMapWindowHost host)
    {
        var context = MapDrawContext.Current; var s = context.GlobalScale;
        var (min, max) = MapShape.Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        if (host.Config.MapCircle) { var inset = (max - min) * .14644661f; min += inset; max -= inset; }
        min += new Vector2(6 * s); max -= new Vector2(6 * s);
        var rowHeight = 26 * s;
        var size = Vector2.Min(new Vector2(246 * s, current.Count * rowHeight + 12 * s), Vector2.Max(new Vector2(1), max - min));
        Min = Vector2.Clamp(anchor, min, Vector2.Max(min, max - size)); Max = Min + size;
        ImGui.SetNextWindowPos(Min); ImGui.SetNextWindowSize(size);
        using var font = context.GetChromeFont(12 * s, ChromeFontRole.Body)?.Push();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(6 * s));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Vector4.Zero);
        try {
            if (!ImGui.BeginPopup(Popup, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings)) { IsOpen = false; return; }
            Capture(); context.Chrome.Reveal(host.Now);
            MapWindowDrawing.DrawSurface(ImGui.GetWindowDrawList(), Min, Max, 10 * s, surfaceOpacity: 1);
            if (ImGui.IsKeyPressed(ImGuiKey.Escape, false) || current.Count == 0) { Close(); ImGui.EndPopup(); return; }
            if (ImGui.IsKeyPressed(ImGuiKey.DownArrow)) selected = (selected + 1) % current.Count;
            if (ImGui.IsKeyPressed(ImGuiKey.UpArrow)) selected = (selected + current.Count - 1) % current.Count;
            var enter = ImGui.IsKeyPressed(ImGuiKey.Enter, false);
            for (var i = 0; i < current.Count; i++) {
                var item = current[i]; var pos = ImGui.GetCursorScreenPos();
                var clicked = ImGui.InvisibleButton("##context-row-" + i, new Vector2(MathF.Max(1, size.X - 12 * s), rowHeight));
                Rows[item.Label] = pos + new Vector2((size.X - 12 * s) / 2, rowHeight / 2);
                var hovered = ImGui.IsItemHovered();
                if (hovered && ImGui.GetIO().MouseDelta != Vector2.Zero) selected = i;
                if (i == selected) ImGui.GetWindowDrawList().AddRectFilled(pos, pos + new Vector2(size.X - 12 * s, rowHeight), ImGui.GetColorU32(new Vector4(1, 1, 1, .09f)), 5 * s);
                GratisIcons.Draw(ImGui.GetWindowDrawList(), pos + new Vector2(13, 13) * s, item.Icon, GratisTheme.Secondary, 14 * s);
                ImGui.GetWindowDrawList().AddText(pos + new Vector2(28, 6) * s, ImGui.GetColorU32(GratisTheme.Primary), item.Label);
                if (enter && selected == i || clicked) { Activate(item); break; }
            }
            ImGui.EndPopup();
        }
        finally { ImGui.PopStyleColor(); ImGui.PopStyleVar(3); }
        if (details.Length > 0) {
            ImGui.BeginTooltip(); ImGui.TextUnformatted(details); ImGui.EndTooltip();
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
        if (toast.Length == 0 || ImGui.GetTime() >= toastUntil) return;
        var context = MapDrawContext.Current; var s = context.GlobalScale;
        using var font = context.GetChromeFont(12 * s, ChromeFontRole.Chip)?.Push();
        var size = ImGui.CalcTextSize(toast) + new Vector2(24, 16) * s;
        var pos = MapAssistance.ConstrainPanel(ImGui.GetWindowPos() + new Vector2((ImGui.GetWindowWidth() - size.X) / 2, ImGui.GetWindowHeight() - 112 * s), size);
        MapWindowDrawing.DrawSurface(ImGui.GetWindowDrawList(), pos, pos + size, 8 * s, surfaceOpacity: 1);
        ImGui.GetWindowDrawList().AddText(pos + new Vector2(12, 8) * s, ImGui.GetColorU32(GratisTheme.Primary), toast);
    }
}
