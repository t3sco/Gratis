using System.Numerics;
using Gratis.Data;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public interface IMapRendererHost
{
    SystemConfig Config { get; }
    double Now { get; }
    int FrameId { get; }
    uint MapId { get; }
    ulong MapKey { get; }
    MapContentBounds? ContentBounds => null;
    bool SmallMapHint => false;
    bool TextureReady => true;
    bool InstanceMap => false;
    long ViewSession => 0;
    Vector2? ViewportOverride => null;
    Vector2 CoordinateOffset(Vector2 position);
    void DrawBackgroundTexture();
    void DrawFogOfWar();
    void CollectMarkers();
    void DrawFlag();
    void DrawPlayer();
    void DrawTextMarkers();
}

public sealed partial class MapRendererFrame(IMapRendererHost host)
{
    public float Scale => host.Config.MapScale;
    public Vector2 DrawOffset { get; set; }
    public Vector2 DrawPosition { get; private set; }
    public void WriteOffset(Vector2 value, [global::System.Runtime.CompilerServices.CallerMemberName] string writer = "")
    {
        var constrained = Constrain(Scale, value);
        host.Config.MapScale = constrained.Scale;
        DrawOffset = constrained.Offset;
        viewportMotion.Written("offset", writer);
    }
    private (float Scale, Vector2 Offset) Constrain(float scale, Vector2 offset) => ViewportMath.Constrain(host.Config,
        viewportSize.X > 1 && viewportSize.Y > 1 ? viewportSize : host.Config.WindowSize,
        viewportTextureSize.X > 1 && viewportTextureSize.Y > 1 ? viewportTextureSize : new Vector2(2048), scale, offset);
    public void SetViewportScale(float scale, bool manual = true)
    {
        ObserveViewIdentity();
        if (manual) UseManualView("explicit zoom");
        else if (host.Config.IsGratis && AutoFit.Mode == ViewportViewMode.Manual) return;
        CancelViewportMotion();
        restoringView = null;
        var constrained = Constrain(scale, DrawOffset);
        WriteScale(constrained.Scale);
        WriteOffset(constrained.Offset);
    }
    public void CenterOnOffset(Vector2 offset, bool manual = true)
    {
        if (manual) UseManualView("explicit pan/centre");
        Spotlight.Cancel("cancelled: explicit centre/zone switch");
        CancelViewportMotion();
        restoringView = null;
        followZoomOffset = Vector2.Zero;
        WriteOffset(offset);
    }
    private void WriteScale(float value, [global::System.Runtime.CompilerServices.CallerMemberName] string writer = "") { host.Config.MapScale = value; viewportMotion.Written("scale", writer); }
    public void CenterOnCoordinate(Vector2 position, bool manual = true)
    {
        var offset = host.CoordinateOffset(position);
        CenterOnOffset(offset, manual);
        PingTexturePosition(new Vector2(1024) - offset);
    }

    private ViewportZoom viewportZoom => viewportMotion.Zoom;
    private Vector2 viewportSize;
    private Vector2 viewportTextureSize = new(2048);
    private Vector2 followTargetOffset;
    private Vector2 followZoomOffset { get => viewportMotion.FollowZoomOffset; set => viewportMotion.FollowZoomOffset = value; }
    private bool wasFollowing;

    public void FollowPlayer(Vector2 position)
    {
        followPlayerPosition = position;
        followTargetOffset = host.CoordinateOffset(position);
        if (Spotlight.OwnsViewport) return;
        if (host.Config.IsGratis && host.Config.FollowReturnToPlayer) {
            followTargetOffset = host.CoordinateOffset(followPlayerPosition);
            return;
        }
        host.Config.Motion ??= new MotionSettings();
        if (!wasFollowing || !host.Config.ClampToMap) followZoomOffset = Vector2.Zero;
        followTargetOffset = host.CoordinateOffset(position);
        if (!MapMotion.Enabled || !host.Config.Motion.EaseFollow)
            WriteOffset(followTargetOffset + followZoomOffset);
    }

    public void PrepareViewport(Vector2 textureSize)
    {
        ObserveViewIdentity();
        if (viewportMotion.HasAdvanced(host.FrameId)) return;
        var validWindow = ValidSize(viewportSize);
        if (host.Config.IsGratis && (!validWindow || !host.TextureReady || !ValidSize(textureSize))) {
            viewportReady = false; AutoFit.Wait(host.Now, validWindow); return;
        }
        if (ValidSize(textureSize)) viewportTextureSize = textureSize;
        viewportReady = true;
        PrepareSmallRoomView();
        PrepareRememberedView();
        PreparePendingSpotlightReturn();
        wasFollowing = host.Config.FollowPlayer;
        MapDrawContext.Current.Trace?.MotionAdvance();
        AdvanceViewportMotion();
        AdvanceRememberedView();
        ApplyAutoFit();
        var constrained = Constrain(Scale, DrawOffset);
        host.Config.MapScale = constrained.Scale;
        DrawOffset = constrained.Offset;
        UpdateDrawOffset();
    }

    public void ZoomViewport(float requestedScale, Vector2 cursorFromCenter, bool animate = true)
    {
        if (host.Config.IsGratis && !viewportReady) return;
        UseManualView("wheel/cluster zoom");
        var config = host.Config;
        viewportMotion.CancelPan();
        if (!animate) CancelViewportMotion();
        if (animate) InteractWithViewport();
        if (animate && QueueViewportZoom(requestedScale, cursorFromCenter)) return;
        if (!config.ClampToMap && !config.IsGratis) {
            WriteScale(requestedScale);
            return;
        }
        var circle = config.MapCircle && config.IsGratis;
        var minimum = ViewportMath.Limits(config, viewportSize, viewportTextureSize).Minimum;
        var scale = Constrain(viewportZoom.Resolve(viewportSize, requestedScale, config.ScaleWithFrame, minimum), DrawOffset).Scale;
        var center = ViewportMath.ZoomAtCursor(new Vector2(1024) - DrawOffset, cursorFromCenter, Scale, scale);
        WriteScale(scale);
        WriteOffset(new Vector2(1024) - ViewportMath.ClampCenter(center,
            viewportSize, viewportTextureSize, scale, circle));
        if (config.FollowPlayer) followZoomOffset = DrawOffset - followTargetOffset;
    }

    public Vector2 ViewportSize => viewportSize;

    public void ZoomToCluster(Vector2 localPosition, float scale)
    {
        var texturePosition = (localPosition - DrawPosition) / scale;
        CancelViewportMotion();
        if (host.Config.IsGratis && host.Config.FollowReturnToPlayer) InteractWithViewport();
        else host.Config.FollowPlayer = false;
        ZoomViewport(scale * 1.8f, Vector2.Zero, false);
        WriteOffset(new Vector2(1024) - texturePosition);
    }
    public readonly ViewportMotion viewportMotion = new();
    private Vector2? pendingPing;
    private uint pendingPingMap;
    private bool followInteracting, followHovered, followFocused;
    private Vector2 followPlayerPosition;

    private void CancelViewportMotion() => viewportMotion.Cancel();

    public void UpdateFollowInteraction(bool interacting, bool hovered, bool focused, Vector2? player = null)
    {
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right)) viewportMotion.CancelPan();
        if (player is { } position) {
            followPlayerPosition = position;
            followTargetOffset = host.CoordinateOffset(position);
        }
        if (Spotlight.Active && ImGui.IsKeyPressed(ImGuiKey.Escape)) Spotlight.End(host.Now, "escape: restoring viewport");
        followInteracting = interacting;
        followHovered = hovered;
        followFocused = focused;
    }

    public void InteractWithViewport(bool drag = false)
    {
        if (host.Config.IsGratis && !viewportReady) return;
        UseManualView(drag ? "drag pan" : "viewport input");
        restoringView = null;
        if (Spotlight.OwnsViewport) { Spotlight.End(host.Now, "input: restoring viewport"); return; }
        host.Config.FollowPlayer = viewportMotion.Interact(host.Config, host.Now, drag);
    }

    public void PanViewport(Vector2 delta)
    {
        if (host.Config.IsGratis && !viewportReady) return;
        if (delta == Vector2.Zero) return;
        InteractWithViewport(true);
        var before = DrawOffset;
        WriteOffset(DrawOffset + delta);
        viewportMotion.SampleDrag(host.Config, DrawOffset - before, host.Now, Scale);
    }

    private bool QueueViewportZoom(float requested, Vector2 cursor) => viewportMotion.QueueZoom(
        host.Config, requested, cursor, Scale, viewportSize, viewportTextureSize, host.Config.UseLinearZoom);

    private void AdvanceViewportMotion()
    {
        if (Spotlight.Request is { } request) {
            viewportMotion.SpotlightTargetTexture = new Vector2(1024) - host.CoordinateOffset(request.Target);
            viewportMotion.SpotlightPlayerTexture = new Vector2(1024) - host.CoordinateOffset(followPlayerPosition);
            Spotlight.UiScale = MapDrawContext.Current.GlobalScale;
        }
        var scale = Scale;
        var offset = DrawOffset;
        var mapKey = host.MapKey;
        viewportMotion.Advance(host.Config, mapKey, host.Now, host.FrameId,
            viewportSize, viewportTextureSize, followPlayerPosition, SmallRoomFollowTarget(followTargetOffset), followInteracting, followHovered,
            followFocused, ref scale, ref offset);
        host.Config.MapScale = scale;
        DrawOffset = offset;
    }

    public string TakeMotionDiagnostics() => ViewportDiagnostics() + " " + viewportMotion.Diagnostics(host.Config, host.Now);

    public void PingTexturePosition(Vector2 position)
    {
        pendingPing = position;
        pendingPingMap = host.MapId;
    }

    private void FlushPendingPing()
    {
        if (pendingPing is not { } position) return;
        if (pendingPingMap == host.MapId)
            MapDrawContext.Current.Motion.PingAt(position, host.Now);
        pendingPing = null;
    }


    public void DrawMapImage(DrawTexture texture)
    {
        PrepareViewport(texture.Size);
        if (host.Config.IsGratis && !viewportReady) { ImGui.Dummy(Vector2.Zero); return; }
        MapDrawContext.Current.Trace?.TexturePass();
        ImGui.SetCursorPos(DrawPosition);
        using var alpha = new MapMotion.AlphaScope(MapDrawContext.Current.Motion.ZoneProgress(host.Now));
        MapTextureDrawing.DrawMapImage(texture, Scale);
    }

    public void DrawBaseTexture()
    {
        MapDrawContext.Current.Spotlight = Spotlight;
        MapDrawContext.Current.SynchronizePvp();
        MapDrawContext.Current.Pvp.BeginFrame(MapDrawContext.Current.IsPvP, host.FrameId);
        MapDrawContext.Current.Motion.BeginMap(host.MapId, host.Now);
        FlushPendingPing();
        viewportSize = host.ViewportOverride ?? ImGui.GetContentRegionAvail();
        ObserveViewIdentity();
        if (MapDrawContext.Current.ReportCapture)
            ImGui.GetWindowDrawList().AddRectFilled(ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize(), 0xff1f1916);
        host.DrawBackgroundTexture();
        if (host.Config.IsGratis && !viewportMotion.HasAdvanced(host.FrameId)) PrepareViewport(Vector2.Zero);
    }

    public void DrawDynamicElements()
    {
        if (host.Config.IsGratis && !viewportReady) return;
        var motion = MapDrawContext.Current.Motion;
        using var alpha = new MapMotion.AlphaScope(motion.ZoneProgress(host.Now));
        host.DrawFogOfWar();
        SpotlightDrawing.DrawMapOverlay(Spotlight, ImGui.GetWindowPos() + DrawPosition, Scale);
        DrawMapMarkers();
        SpotlightDrawing.DrawDistance(Spotlight, ImGui.GetWindowPos() + DrawPosition, Scale);
        motion.DrawPings(ImGui.GetWindowPos() + DrawPosition, Scale, host.Now);
    }

    private void UpdateDrawOffset()
    {
        var offset = DrawOffset;
        var config = host.Config;
        var progress = MapDrawContext.Current.Motion.ZoneProgress(host.Now);
        if (progress < 1) {
            offset += new Vector2((1 - progress) * config.Motion.ZoneSlidePixels / Scale, 0);
            if (config.ClampToMap) offset = new Vector2(1024) - ViewportMath.ClampCenter(new Vector2(1024) - offset,
                viewportSize, viewportTextureSize, Scale, config.MapCircle && config.IsGratis);
        }
        DrawPosition = ViewportMath.DrawPosition(viewportSize, offset, Scale);
    }


    private void DrawMapMarkers()
    {
        var markers = MapDrawContext.Current.Markers;
        MapDrawContext.Current.EdgePointers.Begin(this);
        MapDrawContext.Current.MarkerTargets.Begin(this);
        markers.Begin();
        host.CollectMarkers();
        if (markers.Enabled) host.DrawFlag();
        markers.Flush();
        host.DrawPlayer();
        using (var labelsAlpha = FrameScopes.PushStyle(ImGuiStyleVar.Alpha,
                   ImGui.GetStyle().Alpha * (1 - .6f * Spotlight.Opacity), Spotlight.Opacity > 0))
            host.DrawTextMarkers();
        if (!markers.Enabled) host.DrawFlag();
    }
}
