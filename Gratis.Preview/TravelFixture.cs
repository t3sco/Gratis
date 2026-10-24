using System.Numerics;
using Gratis.Controllers;
using Gratis.Rendering;

namespace Gratis.Preview;

internal sealed class TravelFixture(ScriptedFrameDriver driver) : ITravelPlaces
{
    public static readonly PlaceEntry Hub = new("a:1", PlaceKind.Aetheryte, "Limsa Lominsa", "Limsa Lominsa Lower Decks", 1, 1, new(-150, 20), 1, 1);
    public static readonly PlaceEntry Shard = new("a:10", PlaceKind.Aetheryte, "Aftcastle", "Limsa Lominsa Lower Decks", 1, 1, new(140, -90), 10, 1);
    public IEnumerable<PlaceEntry> Entries => [Hub, Shard];
    public PlaceSearchContext Context => new(driver.MapId, driver.TerritoryId, driver.PlayerPosition);
    public PlaceAccess Access(PlaceEntry place) => new(true, place.IsShard ? 0u : 320u);
    public void Refresh() { }
    public int Teleports { get; private set; }
    public string Teleport(PlaceEntry place, bool confirmed) { Teleports++; return "Teleport requested."; }
    public void Show(PlaceEntry place) {
        if (place.World is { } world) driver.Renderer.BeginSpotlight(new(world, place.MapId, SpotlightSource.Search));
    }
}

internal sealed class FakeTravelIpc
{
    public string? Version = "2.6.0.8";
    public bool Busy, Failing;
    public uint Active = 1;
    public int Requests, Cancels;
    public LifestreamTravelProvider Provider() => new(() => Version,
        () => Failing ? throw new InvalidOperationException("test IPC failure") : Busy,
        () => Active, _ => { Requests++; Busy = true; return true; },
        (_, _) => { Requests++; Busy = true; return true; }, () => { Cancels++; Busy = false; });
}
