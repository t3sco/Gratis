namespace Gratis.Rendering;

public sealed partial class MapAssistance
{
    private SpotlightRequest? contextTarget;
    private double contextTargetUntil;
    public void OfferContextTarget(SpotlightRequest target, double now)
    {
        contextTarget = target; contextTargetUntil = now + 12; dismissedTarget = chipTarget = null; chipConfirm = false; ChipMessage = "";
    }
    private SpotlightRequest? ContextTarget(IMapWindowHost host)
    {
        if (host.Now > contextTargetUntil || contextTarget?.MapId != host.Renderer.ViewedMapId) contextTarget = null;
        return contextTarget;
    }
    private bool IsContextTarget(SpotlightRequest target) => contextTarget == target;
}
