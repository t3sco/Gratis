using System.Numerics;

namespace Gratis.Rendering;

public sealed partial class MapRendererFrame {
    public Spotlight Spotlight => viewportMotion.Spotlight;
    private bool pendingSpotlightReturn;

    public bool BeginSpotlight(SpotlightRequest request) {
        if (MapDrawContext.Current.IsPvP && request.Source == SpotlightSource.Search) {
            Spotlight.Cancel("PvP: search spotlight disabled");
            Spotlight.Reject(request, "PvP: search spotlight disabled");
            return false;
        }
        if (!host.Config.IsGratis || request.MapId != host.MapId || !float.IsFinite(request.Target.X)
            || !float.IsFinite(request.Target.Y)) {
            Spotlight.Reject(request, "normal behaviour: unavailable map/target/style");
            return false;
        }
        RememberSpotlightReturn();
        var replacing = Spotlight.OwnsViewport;
        viewportMotion.Cancel();
        viewportMotion.Follow.Interact(host.Config, host.Now, false);
        Spotlight.Begin(request, host.Config, host.MapKey, Scale, DrawOffset,
            viewportSize.X > 0 && viewportSize.Y > 0 ? viewportSize : host.Config.WindowSize);
        pendingSpotlightReturn = !viewportReady;
        if (!replacing && viewportReady && host.Config.RememberMapView
            && ViewMemory.Find(host.Config, host.MapId) is { } saved
            && saved.CanRestore(host.MapKey, host.ViewSession, host.InstanceMap, viewportTextureSize))
            Spotlight.RememberReturn(saved, host.Config, viewportSize.X > 0 ? viewportSize : host.Config.WindowSize,
                viewportTextureSize);
        return true;
    }

    private void PreparePendingSpotlightReturn() {
        if (!pendingSpotlightReturn || !viewportReady)
            return;
        pendingSpotlightReturn = false;
        if (Spotlight.Request?.MapId == host.MapId && host.Config.RememberMapView
            && ViewMemory.Find(host.Config, host.MapId) is { } saved
            && saved.CanRestore(host.MapKey, host.ViewSession, host.InstanceMap, viewportTextureSize))
            Spotlight.RememberReturn(saved, host.Config, viewportSize, viewportTextureSize);
    }

    public bool BeginQuestSpotlight(Vector2? target, uint mapId, bool sameMap, uint? icon = null) {
        var request = new SpotlightRequest(target ?? Vector2.Zero, mapId, SpotlightSource.Quest, Icon: icon,
            IncludePlayer: sameMap && host.Config.SpotlightIncludePlayer);
        if (!host.Config.QuestSpotlight || target is null) {
            Spotlight.Cancel("normal behaviour: disabled or marker not found");
            Spotlight.Reject(request, "normal behaviour: disabled or marker not found");
            return false;
        }
        return BeginSpotlight(request);
    }
}