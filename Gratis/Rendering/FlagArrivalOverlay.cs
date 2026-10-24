using Gratis.Controllers;

namespace Gratis.Rendering;

public partial class MapDrawingConfig
{
    public bool FlagArrival = true;
    public bool FlagAutoClear = false;
    public float FlagArrivalRadius = 4;
}

public readonly record struct FlagArrivalSample(uint Territory, uint Map, global::System.Numerics.Vector2? Player,
    ArrivalFlag? Flag, string Place, bool Transitioning = false, long ZoneRevision = 0);

public interface IFlagArrivalSource
{
    FlagArrivalSample Read();
}

public sealed class FlagArrivalOverlay
{
    public FlagArrivalState State { get; } = new();
    public IFlagArrivalSource? Source { get; set; }
    private uint viewedMap;
    public void Update(IMapWindowHost host)
    {
        viewedMap = host.Renderer.ViewedMapId;
        if (Source == null) return;
        var sample = Source.Read();
        if (State.Update(host.Now, sample.Territory, sample.Map, sample.Player, sample.Flag,
            host.Config.IsGratis && host.Config.FlagArrival, host.Config.FlagArrivalRadius,
            sample.Transitioning, sample.ZoneRevision) && sample.Flag is { } flag)
            Notify(host, flag, sample.Place);
    }
    public void Notify(IMapWindowHost host, ArrivalFlag flag, string place)
    {
        var context = MapDrawContext.Current;
        var texture = (host.Renderer.WorldToScreen(flag.World, global::System.Numerics.Vector2.Zero) - host.Renderer.DrawPosition) / host.Renderer.Scale;
        if (viewedMap == flag.Map) context.Motion.PingAt(texture, host.Now);
        context.ContextMenu.Notify("Arrived: " + place);
    }
    public float FlagAlpha => MapDrawContext.Current.Config is { IsGratis: true, FlagArrival: true } && State.Arrived && State.Flag?.Map == viewedMap ? .45f : 1;
}
