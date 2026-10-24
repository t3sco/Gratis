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
    public void WriteOffset(Vector2 value, [global::System.Runtime.CompilerServices.CallerMemberName] string writer = "") { DrawOffset = value; viewportMotion.Written("offset", writer); }
    private void WriteScale(float value, [global::System.Runtime.CompilerServices.CallerMemberName] string writer = "") { host.Config.MapScale = value; viewportMotion.Written("scale", writer); }
    public void CenterOnCoordinate(Vector2 position)
    {
        Spotlight.Cancel("cancelled: explicit centre/zone switch");
        CancelViewportMotion();
        followZoomOffset = Vector2.Zero;
        WriteOffset(host.CoordinateOffset(position));
        PingTexturePosition(new Vector2(1024) - DrawOffset);
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
        if (viewportMotion.HasAdvanced(host.FrameId)) return;
        if (textureSize.X > 0 && textureSize.Y > 0) viewportTextureSize = textureSize;
        wasFollowing = host.Config.FollowPlayer;
        MapDrawContext.Current.Trace?.MotionAdvance();
        AdvanceViewportMotion();
        UpdateDrawOffset();
    }

    public void ZoomViewport(float requestedScale, Vector2 cursorFromCenter, bool animate = true)
    {
        var config = host.Config;
        if (animate) InteractWithViewport();
        if (animate && QueueViewportZoom(requestedScale, cursorFromCenter)) return;
        if (!config.ClampToMap) {
            WriteScale(requestedScale);
            return;
        }
        var circle = config.MapCircle && config.IsGratis;
        var minimum = ViewportMath.MinimumScale(viewportSize, viewportTextureSize, circle);
        if (requestedScale <= minimum && Scale <= minimum) return;
        var scale = viewportZoom.Resolve(viewportSize, requestedScale, config.ScaleWithFrame, minimum);
        var center = ViewportMath.ZoomAtCursor(new Vector2(1024) - DrawOffset, cursorFromCenter, Scale, scale);
        WriteOffset(new Vector2(1024) - ViewportMath.ClampCenter(center,
            viewportSize, viewportTextureSize, scale, circle));
        if (config.FollowPlayer) followZoomOffset = DrawOffset - followTargetOffset;
        WriteScale(scale);
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
        if (Spotlight.OwnsViewport) { Spotlight.End(host.Now, "input: restoring viewport"); return; }
        host.Config.FollowPlayer = viewportMotion.Interact(host.Config, host.Now, drag);
    }

    public void PanViewport(Vector2 delta)
    {
        InteractWithViewport(true);
        WriteOffset(DrawOffset + delta);
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
            viewportSize, viewportTextureSize, followPlayerPosition, followTargetOffset, followInteracting, followHovered,
            followFocused, ref scale, ref offset);
        host.Config.MapScale = scale;
        DrawOffset = offset;
    }

    public string TakeMotionDiagnostics() => viewportMotion.Diagnostics(host.Config, host.Now);

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
        MapDrawContext.Current.Trace?.TexturePass();
        ImGui.SetCursorPos(DrawPosition);
        using var alpha = new MapMotion.AlphaScope(MapDrawContext.Current.Motion.ZoneProgress(host.Now));
        MapTextureDrawing.DrawMapImage(texture, Scale);
    }

    public void DrawBaseTexture()
    {
        MapDrawContext.Current.Spotlight = Spotlight;
        MapDrawContext.Current.Motion.BeginMap(host.MapId, host.Now);
        FlushPendingPing();
        viewportSize = ImGui.GetContentRegionAvail();
        host.DrawBackgroundTexture();
    }

    public void DrawDynamicElements()
    {
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
