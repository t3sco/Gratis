using System.Numerics;
using Gratis.Classes;
using Gratis.Data;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public interface IMapWindowHost
{
    SystemConfig Config { get; }
    MapRendererFrame Renderer { get; }
    Vector2? PlayerPosition { get; }
    double Now { get; }
    bool SettingsOpen { get; }
    bool ShouldFade { get; }
    bool PlayerMoving => false;
    bool MapFocused => false;
    SubZoneLocation? SubZone => null;
    MapLocation? Location => null;
    string Title { get; }
    ImGuiWindowFlags Flags { get; set; }
    PaletteOverlay? Palette => null;
    void UpdateWindow();
    MoveWorkArea MoveWorkArea => MoveWorkAreaAt(ImGui.GetMousePos());
    MoveWorkArea MoveWorkAreaAt(Vector2 point) => MoveHandleScreen.At(point);
    Vector2 MinimumWindowSize => new Vector2(400, 250) * MapDrawContext.Current.GlobalScale;
    void SaveWindowPosition() { }
    void DrawSpoilerWarning();
    void DrawToolbar();
    void DrawCoordinates(bool mapHovered, Vector2 offset);
    void DrawContextMenu(Vector2 offset);
}

public sealed partial class MapWindowFrame
{
    public Vector2 MapDrawOffset { get; private set; }
    public HoverFlags HoveredFlags { get; private set; }
    private bool isDragStarted;
    private Vector2 lastWindowSize;
    public bool Dragging => isDragStarted;
    public MoveHandleInput MoveHandle { get; } = new();
    public void ResetWindowInput() { isDragStarted = false; MoveHandle.Cancel(); }
    public SubZoneBanner ZoneBanner { get; } = new();
    public void PreDraw(IMapWindowHost host)
    {
        MapDrawContext.Current.WindowPresets.PrepareWindow(host, MoveHandle.Dragging || isDragStarted);
        MoveHandle.PrepareWindow(host); PrepareGhost(host);
    }
    private static bool IsBoundedBy(Vector2 cursor, Vector2 min, Vector2 max) =>
        cursor.X >= min.X && cursor.Y >= min.Y && cursor.X <= max.X && cursor.Y <= max.Y;

    public void Draw(IMapWindowHost host)
    {
        MapDrawContext.Current.Trace?.BeginFrame(ImGui.GetFrameCount());
        host.UpdateWindow();
        if (host.Config.IsGratis) UiMotion.Begin();
        UiMotion.IncludeGhost(Ghost.AnimationActive, Ghost.AnimationKeys);
        ZoneBanner.Update(host.Config, host.SubZone, host.Now);
        MapDrawContext.Current.Measure.BeginFrame();
        MapDrawContext.Current.ContextMenu.Measure ??= MapDrawContext.Current.Measure;
        MapDrawContext.Current.Assistance?.Prepare(host.Config, host.PlayerMoving);
        host.Palette?.Prepare(host.Config, ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows));
        var paletteInput = host.Palette?.CapturesInput == true;
        MapDrawContext.Current.ContextMenu.Prepare(host.Config);
        var assistanceInput = MapDrawContext.Current.Assistance?.InputActive == true || MapDrawContext.Current.ContextMenu.CapturesInput;
        var filterInput = host.Config.IsGratis && !MoveHandle.Dragging &&
            FilterBarDrawing.PointerOver;
        var measureGesture = MapDrawContext.Current.Measure.WantsGesture(host.Config);
        if (paletteInput || assistanceInput || measureGesture) MoveHandle.Cancel();
        else if (!filterInput) MoveHandle.Update(host, isDragStarted);
        var moveBlocked = !filterInput && MoveHandle.BlocksMapInput;
        MapDrawContext.Current.WindowMoveInputBlocked = moveBlocked || paletteInput || assistanceInput;
        if (moveBlocked || paletteInput || assistanceInput) isDragStarted = false;
        if (host.Config.FollowPlayer && host.PlayerPosition is { } player) host.Renderer.FollowPlayer(player);
        if (host.Config.LockCenterOnMap && !host.Renderer.Spotlight.OwnsViewport) {
            host.Config.FollowPlayer = false;
            host.Renderer.DrawOffset = Vector2.Zero;
        }

        HoveredFlags = HoverFlags.Nothing;

        if (IsBoundedBy(ImGui.GetMousePos(), ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + DrawBinding.ContentRegionMax())) {
            HoveredFlags |= HoverFlags.Window;
        }

        if (!host.Config.IsGratis && host.Config.BlurBackground) {
            var blurMin = ImGui.GetCursorScreenPos();
            MapWindowDrawing.DrawWindowBackgroundBlur(ImGui.GetWindowDrawList(), blurMin, blurMin + ImGui.GetContentRegionAvail(),
                MapShape.Rounding(ImGui.GetContentRegionAvail()), true, host.Config.BlurStrength);
        }

        MapDrawOffset = ImGui.GetCursorScreenPos();
        var ghostEnabled = host.Config.IsGratis && host.Config.GhostMode;
        var restoreHover = host.Config.IsGratis && host.Config.RestoreOpacityOnHover &&
            ImGui.IsMouseHoveringRect(ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize(), false);
        var fadedTarget = host.ShouldFade && !restoreHover ? host.Config.FadePercent : 1;
        var opacity = ghostEnabled ? Ghost.Opacity : host.Config.IsGratis && host.Config.RestoreOpacityOnHover
            ? UiMotion.Value(ImGui.GetID("##hover-unfade"), 9, fadedTarget,
                restoreHover ? InterfaceMotionTokens.HoverEnter : InterfaceMotionTokens.HoverExit, opacity: true)
            : host.ShouldFade ? host.Config.FadePercent : 1;
        using var fade = FrameScopes.PushStyle(ImGuiStyleVar.Alpha, opacity, host.Config.IsGratis || host.ShouldFade);
        using (var renderChild = FrameScopes.Child("render_child", ImGui.GetContentRegionAvail(), ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar |
            (Ghost.ClickThrough ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None))) {
            if (!renderChild.Visible) return;
            MapShape.Update(ImGui.GetWindowSize());
            if (!host.Config.AcceptedSpoilerWarning) {
                host.DrawSpoilerWarning();
                return;
            }

            var mouseOverFrame = !paletteInput && ChromeVisibility.IsMapFrameHovered();
            var activity = ChromeActivity.None;
            if (moveBlocked) activity |= ChromeActivity.WindowMove;
            else if (mouseOverFrame) activity |= ChromeActivity.Hover;
            if (isDragStarted) activity |= ChromeActivity.Drag;
            if (host.SettingsOpen) activity |= ChromeActivity.Settings;
            if (!paletteInput && !moveBlocked && ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows) && ImGui.IsAnyItemActive())
                activity |= ChromeActivity.FocusedActiveItem;
            MapDrawContext.Current.Chrome.Update(host.Config, host.Now, activity,
                parentAlpha: ImGui.GetStyle().Alpha);
            if (host.Config.IsGratis) {
                var (min, max) = MapShape.Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
                MapWindowDrawing.DrawWindowBackgroundBlur(ImGui.GetWindowDrawList(), min, max, MapShape.Current,
                    host.Config.BlurBackground, host.Config.BlurStrength);
            }
            var mapFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows);
            var mapInteracting = !paletteInput && !MapDrawContext.Current.EdgePointers.PointerOver && MapDrawContext.Current.Assistance?.InputActive != true && (isDragStarted || (mouseOverFrame &&
                (ImGui.IsAnyMouseDown() || ImGui.IsAnyItemActive() || ImGui.GetIO().MouseWheel != 0)));
            host.Renderer.UpdateFollowInteraction(!moveBlocked && mapInteracting,
                !moveBlocked && mouseOverFrame, mapFocused, host.PlayerPosition);
            if (host.Config.IsGratis) MapDrawContext.Current.Visibility.BeginFrame(MapDrawContext.Current);
            using (var moveInput = new MoveHandleInput.InputScope(moveBlocked)) {
            using (var measureInput = new MoveHandleInput.InputScope(measureGesture || paletteInput)) DrawMapElements(host);

            ImGui.SetCursorPos(Vector2.Zero);
            ImGui.Dummy(Vector2.Zero);
            if (host.Config.IsGratis) DrawFrameEffects();
            MapDrawContext.Current.Trace?.Chrome("toolbar-call");
            DrawToolbar(host);
            using (var paletteBlock = new MoveHandleInput.InputScope(paletteInput)) {
            MapDrawContext.Current.Trace?.Chrome("coordinates-call");
            if (host.Config.IsGratis) BottomCardsDrawing.Prepare(host.Title, host.SubZone, host.Location);
            DrawCoordinateBar(host);
            if (!host.Config.IsGratis) DrawFrameEffects();
            MapDrawContext.Current.Trace?.Chrome("title-call");
            MapWindowDrawing.DrawTitleOnMap(host.Title);
            SubZoneBannerDrawing.Draw(ZoneBanner);
            if (host.Config.IsGratis) FilterBarDrawing.Draw();
            MapDrawContext.Current.Assistance?.Draw(host);
            MoveHandle.Draw(host.Config);
            MapDrawContext.Current.Measure.Draw(host, MoveHandle.Geometry);
            MapDrawContext.Current.EdgePointers.Draw(host);
            }
            }
            if (!MapDrawContext.Current.Measure.InputActive) MapDrawContext.Current.ContextMenu.Draw(host, MapDrawOffset);
            host.Palette?.DrawHint(host.Config);
            host.Palette?.Draw(host.Config);
            DrawGhostHint(host);
            HoverCardDrawing.DrawTail();
            UiMotion.DrawTooltipTail();
        }

        if (ImGui.IsItemHovered()) {
            HoveredFlags |= HoverFlags.WindowInnerFrame;
        }

        ProcessInputs(host);
    }

    private void DrawMapElements(IMapWindowHost host)
    {
        host.Renderer.DrawBaseTexture();
        MapDrawContext.Current.FlagArrival.Update(host);
        if (ImGui.IsItemHovered()) {
            HoveredFlags |= HoverFlags.MapTexture;
        }

        LensDrawing.DrawMapHint();
        host.Renderer.DrawDynamicElements();
        MapDrawContext.Current.Assistance?.PinLayer?.DrawMarkers(host);
    }

    private void DrawToolbar(IMapWindowHost host)
    {
        if (!ShouldShowToolbar(host)) return;

        using (FrameScopes.Group()) {
            host.DrawToolbar();
        }

        if (ImGui.IsItemHovered()) {
            HoveredFlags |= HoverFlags.Toolbar;
        }
    }

    private void DrawCoordinateBar(IMapWindowHost host)
    {
        if (!host.Config.ShowCoordinateBar) return;

        using (FrameScopes.Group()) {
            host.DrawCoordinates(host.Config.IsGratis ? ChromeVisibility.IsMapFrameHovered() : HoveredFlags.HasFlag(HoverFlags.MapTexture), MapDrawOffset);
        }

        if (ImGui.IsItemHovered()) {
            HoveredFlags |= HoverFlags.CoordinateBar;
        }
    }

    private void DrawFrameEffects() => MapWindowDrawing.DrawFrameEffects();


    private bool ShouldShowToolbar(IMapWindowHost host)
    {
        if (host.Config.IsGratis && host.Palette?.IsOpen == true) return true;
        if (host.Config.IsGratis && (host.Config.AutoHideChrome || host.Config.PinChrome))
            return MapDrawContext.Current.Chrome.ControlsAlpha(host.Config) * ImGui.GetStyle().Alpha >= .05f;
        if (isDragStarted) return false;
        if (host.Config.ShowToolbarOnHover && HoveredFlags.Any()) return true;
        if (host.Config.AlwaysShowToolbar) return true;

        return false;
    }

    private void ProcessInputs(IMapWindowHost host)
    {
        if (host.Config.IsGratis && MapDrawContext.Current.Measure.InputActive) { isDragStarted = false; host.Flags |= ImGuiWindowFlags.NoMove; return; }
        if (host.Config.IsGratis && MapDrawContext.Current.EdgePointers.InputActive) { isDragStarted = false; host.Flags |= ImGuiWindowFlags.NoMove; return; }
        if (host.Config.IsGratis && (MapDrawContext.Current.Assistance?.InputActive == true || MapDrawContext.Current.ContextMenu.CapturesInput || CoordinateCopy.InputActive)) { isDragStarted = false; return; }
        if (host.Config.IsGratis && FilterBarDrawing.InputActive) { isDragStarted = false; return; }
        if (MoveHandle.BlocksMapInput || host.Palette?.CapturesInput == true || host.Palette?.IsOpen == true) {
            isDragStarted = false;
            host.Flags |= ImGuiWindowFlags.NoMove;
            return;
        }
        if (host.Config.IsGratis && host.Config.GratisContextMenu) {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Right)) { isDragStarted = false; return; }
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
            ImGui.OpenPopup("Gratis_Context_Menu");
        }
        else {
            if (HoveredFlags.Any()) {
                if (!host.Config.LockWindow && host.Config.EnableShiftDragMove && ImGui.GetIO().KeyShift) {
                    host.Flags &= ~ImGuiWindowFlags.NoMove;
                }
                else {
                    ProcessMouseScroll(host);
                    ProcessMapDragStart(host);
                    host.Flags |= ImGuiWindowFlags.NoMove;
                }
            }

            ProcessMapDragDragging(host);
            ProcessMapDragEnd(host);
        }

        if (!host.Config.IsGratis || !host.Config.GratisContextMenu) host.DrawContextMenu(MapDrawOffset);
    }

    private void ProcessMouseScroll(IMapWindowHost host)
    {
        if (host.Config.ZoomLocked) return;
        if (ImGui.GetIO().MouseWheel is 0) return;
        if (!HoveredFlags.HasFlag(HoverFlags.WindowInnerFrame)) return;

        var scale = host.Config.MapScale;
        var wheel = host.Config.ZoomSpeed * ImGui.GetIO().MouseWheel;
        var requestedScale = host.Config.UseLinearZoom ? scale + wheel : scale * (1.0f + wheel);
        host.Renderer.ZoomViewport(requestedScale,
            ImGui.GetMousePos() - MapDrawOffset - host.Renderer.ViewportSize / 2);
    }

    private void ProcessMapDragDragging(IMapWindowHost host)
    {
        if (ImGui.IsMouseDragging(ImGuiMouseButton.Left) && isDragStarted) {
            host.Renderer.PanViewport(ImGui.GetMouseDragDelta() / host.Config.MapScale);
            ImGui.ResetMouseDragDelta();
        }
    }

    private void ProcessMapDragEnd(IMapWindowHost host)
    {
        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left)) {
            isDragStarted = false;
        }
    }

    private void ProcessMapDragStart(IMapWindowHost host)
    {
        if (ImGui.GetWindowSize() == lastWindowSize) {
            var pressed = host.Config.IsGratis ? ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
                ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem) &&
                (!HoveredFlags.HasFlag(HoverFlags.Toolbar) || MapToolbarDrawing.SearchExitPointerOver) : ImGui.IsItemClicked(ImGuiMouseButton.Left);
            if (pressed && !isDragStarted) {
                isDragStarted = true;
                host.Renderer.InteractWithViewport(true);
            }
        }
        else {
            lastWindowSize = ImGui.GetWindowSize();
            isDragStarted = false;
        }
    }

}
