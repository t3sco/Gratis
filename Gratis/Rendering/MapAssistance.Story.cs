using Gratis.Controllers;

namespace Gratis.Rendering;

public sealed partial class MapAssistance {
    private SpotlightRequest? storyTarget;
    private uint storyViewedMap;
    private double storyUntil;
    public void OfferStoryTarget(PlaceEntry objective, uint viewedMap, double now) {
        if (objective.World is not { } world)
            return;
        storyTarget = new(world, objective.MapId, SpotlightSource.Quest, Icon: objective.Icon, IncludePlayer: false);
        storyViewedMap = viewedMap;
        storyUntil = now + 12;
    }

    public void ClearStoryTarget() => storyTarget = null;
    private SpotlightRequest? StoryTarget(IMapWindowHost host) {
        if (host.Now > storyUntil || host.Renderer.ViewedMapId != storyViewedMap)
            storyTarget = null;
        return storyTarget;
    }
}