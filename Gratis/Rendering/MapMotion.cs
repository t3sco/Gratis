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

public sealed class MapMotion
{
    public readonly struct AlphaScope : IDisposable
    {
        private readonly bool pushed;
        public AlphaScope(float progress)
        {
            pushed = Enabled && progress < 1;
            if (pushed) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * (.35f + .65f * progress));
        }
        public void Dispose() { if (pushed) ImGui.PopStyleVar(); }
    }
    private readonly record struct Ping(Vector2 Position, double Started);
    private readonly record struct Observation(uint Icon, uint Revision, double Seen);
    private readonly Ping[] pings = new Ping[16];
    private readonly Dictionary<ulong, Observation> observations = new(4096);
    private readonly List<ulong> expired = new(4096);
    public PlayerTrail Trail { get; } = new();
    private int nextPing;
    private uint? map;
    private double zoneStarted = double.NegativeInfinity;
    private double lastTime = double.NegativeInfinity;
    private double lastPrune;
    private int firstFrame;
    private bool wasEnabled;

    public static bool IsEnabled(MapDrawingConfig config) => config is { IsGratis: true, ReduceMotion: false };
    public static bool Enabled => IsEnabled(MapDrawContext.Current.Config);

    public void BeginMap(uint id, double now)
    {
        if (map != id || now < lastTime) {
            zoneStarted = map.HasValue && now >= lastTime ? now : double.NegativeInfinity;
            map = id;
            observations.Clear();
            Trail.Clear();
            Array.Clear(pings);
            firstFrame = ImGui.GetFrameCount();
        }
        if (Enabled != wasEnabled) {
            observations.Clear();
            Trail.Clear();
            Array.Clear(pings);
            firstFrame = ImGui.GetFrameCount();
            zoneStarted = double.NegativeInfinity;
            wasEnabled = Enabled;
        }
        if (!Enabled) {
            Array.Clear(pings);
            Trail.Clear();
            observations.Clear();
            zoneStarted = double.NegativeInfinity;
            firstFrame = ImGui.GetFrameCount();
        }
        lastTime = now;
    }

    public float ZoneProgress(double now)
    {
        var config = MapDrawContext.Current.Config;
        return Enabled && config.Motion.ZoneTransition ? MotionMath.Transition(now, zoneStarted, config.Motion.ZoneSeconds) : 1;
    }

    public void PingAt(Vector2 texturePosition, double now)
    {
        if (!Enabled || !MapDrawContext.Current.Config.Motion.Pings) return;
        for (var i = 0; i < pings.Length; i++)
            if (pings[i] != default && now >= pings[i].Started && now - pings[i].Started < .1 && Vector2.DistanceSquared(pings[i].Position, texturePosition) < 1) return;
        pings[nextPing++ % pings.Length] = new Ping(texturePosition, now);
    }

    public void Observe(MarkerInfo marker)
    {
        if (!Enabled || !MapDrawContext.Current.Config.Motion.Pings || marker.Scale <= 0 ||
            !(marker.IsDynamicMarker && FlatMarkerDrawing.Category(marker) == MarkerCategory.Objective || marker.IsFlag)) return;
        var position = marker.Position / marker.Scale;
        var key = ((ulong)(marker.ObjectiveId ?? marker.DataId ?? 0) << 32) ^
            ((ulong)(uint)MathF.Round(position.X) << 16) ^ (uint)MathF.Round(position.Y) ^ ((ulong)marker.MarkerType << 56);
        var now = ImGui.GetTime();
        var found = observations.TryGetValue(key, out var previous);
        if (found ? previous.Icon != marker.IconId || previous.Revision != marker.MotionRevision : ImGui.GetFrameCount() > firstFrame)
            PingAt(position, now);
        if (observations.Count >= 4096) observations.Clear();
        observations[key] = new Observation(marker.IconId, marker.MotionRevision, now);
        if (now - lastPrune <= 1) return;
        expired.Clear();
        foreach (var item in observations)
            if (now - item.Value.Seen > 2) expired.Add(item.Key);
        foreach (var stale in expired) observations.Remove(stale);
        lastPrune = now;
    }

    private static bool Contains(Vector2 center, float radius) => MapShape.ContainsSymbol(center, new Vector2(radius)) &&
        MapShape.Contains(center - new Vector2(radius)) && MapShape.Contains(center + new Vector2(radius)) &&
        MapShape.Contains(center + new Vector2(radius, -radius)) && MapShape.Contains(center + new Vector2(-radius, radius));

    public void DrawTrail(Vector2 origin, float zoom, double now)
    {
        var config = MapDrawContext.Current.Config;
        if (!Enabled || !config.Motion.PlayerTrail) { Trail.Clear(); return; }
        Trail.Prune(now, config.Motion.TrailSeconds);
        var list = ImGui.GetWindowDrawList();
        var scale = MapDrawContext.Current.GlobalScale;
        for (var i = 0; i < Trail.Count; i++) {
            var sample = Trail[i];
            var center = origin + sample.Position * zoom;
            if (!Contains(center, 3 * scale)) continue;
            var alpha = MotionMath.TrailAlpha(now, sample.Time, config.Motion.TrailSeconds) * config.Motion.TrailOpacity;
            list.AddCircleFilled(center, (2 + alpha * 3) * scale,
                ImGui.GetColorU32(new Vector4(.025f, .03f, .045f, alpha)), 12);
            list.AddCircleFilled(center, (1 + alpha * 3) * scale, ImGui.GetColorU32(config.AccentColor with { W = alpha }), 12);
        }
    }

    public void DrawPings(Vector2 origin, float zoom, double now)
    {
        var config = MapDrawContext.Current.Config;
        if (!Enabled || !config.Motion.Pings) { Array.Clear(pings); return; }
        var list = ImGui.GetWindowDrawList();
        var scale = MapDrawContext.Current.GlobalScale;
        for (var i = 0; i < pings.Length; i++) {
            if (pings[i] == default) continue;
            var center = origin + pings[i].Position * zoom;
            for (var ring = 0; ring < 2; ring++) {
                var (radius, alpha) = MotionMath.Ring((float)(now - pings[i].Started), ring, config.Motion.PingSeconds);
                radius *= scale;
                if (alpha <= 0 || !Contains(center, radius + 2 * scale)) continue;
                list.AddCircle(center, radius, ImGui.GetColorU32(config.AccentColor with { W = alpha }), 48, 1.7f * scale);
            }
        }
    }
}
