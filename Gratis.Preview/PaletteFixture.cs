using Gratis.Controllers;
using Gratis.Rendering;

namespace Gratis.Preview;

internal sealed class PaletteFixture : IPaletteSource {
    private readonly ScriptedFrameDriver driver;
    public PaletteOverlay Overlay { get; }
    public PaletteFixture(ScriptedFrameDriver driver) {
        this.driver = driver;
        Overlay = new(this);
    }
    public static readonly PlaceEntry[] Places = [
        new("z:1", PlaceKind.Zone, "Limsa Lominsa Lower Decks", "La Noscea > Limsa Lominsa", 1, 1, Aliases: "リムサ・ロミンサ"),
        new("z:2", PlaceKind.Zone, "Limsa Lominsa Upper Decks", "La Noscea > Limsa Lominsa", 2, 1),
        new("a:1", PlaceKind.Aetheryte, "Limsa Lominsa", "La Noscea > Limsa Lominsa Lower Decks", 1, 1, new(30, 20), 1,
            1, "aetheryte teleport リムサ・ロミンサ"),
        new("a:2", PlaceKind.Aetheryte, "Summerford Farms", "La Noscea > Middle La Noscea", 3, 2, new(100, 200), 2, 2,
            "aetheryte teleport"),
        new("a:3", PlaceKind.Aetheryte, "Aleport", "La Noscea > Western La Noscea", 4, 3, new(-100, 160), 3, 3,
            "aetheryte teleport"),
        new("a:4", PlaceKind.Aetheryte, "Horizon", "Thanalan > Western Thanalan", 5, 4, new(100, 100), 4, 4,
            "aetheryte teleport"),
        new("a:5", PlaceKind.Aetheryte, "Camp Bronze Lake", "La Noscea > Upper La Noscea", 6, 5, new(200, -300), 5, 5,
            "aetheryte teleport"),
        new("a:6", PlaceKind.Aetheryte, "Idyllshire", "Dravania > Idyllshire", 7, 6, new(50), 6, 6,
            "aetheryte teleport イディルシャイア", 1),
        new("a:7", PlaceKind.Aetheryte, "The Crystarium", "Norvrandt > Lakeland", 8, 7, new(500), 7, 7,
            "aetheryte teleport", 3),
        new("a:8", PlaceKind.Aetheryte, "Old Sharlayan", "The Northern Empty > Old Sharlayan", 9, 8, new(40), 8, 8,
            "aetheryte teleport", 4),
        new("a:9", PlaceKind.Aetheryte, "Tuliyollal", "Yok Tural > Tuliyollal", 10, 9, new(20), 9, 9,
            "aetheryte teleport", 5),
        new("a:10", PlaceKind.Aetheryte, "Hawkers' Alley", "La Noscea > Limsa Lominsa > Limsa Lominsa Aetheryte", 1, 1,
            new(-50, 90), 10, 1, "aethernet shard"),
        new("z:7", PlaceKind.Zone, "Idyllshire", "Dravania > Idyllshire", 7, 6, Expansion: 1),
        new("z:11", PlaceKind.Zone, "The Azim Steppe", "Othard > The Azim Steppe", 11, 10, Expansion: 2),
        new("z:8", PlaceKind.Zone, "Lakeland", "Norvrandt > Lakeland", 8, 7, Expansion: 3),
        new("z:9", PlaceKind.Zone, "Old Sharlayan", "The Northern Empty > Old Sharlayan", 9, 8, Expansion: 4),
        new("z:10", PlaceKind.Zone, "Tuliyollal", "Yok Tural > Tuliyollal", 10, 9, Expansion: 5),
        new("q:1", PlaceKind.Quest, "It's Probably Pirates", "La Noscea > Limsa Lominsa", 1, 1, new(140, -50)),
        new("m:1", PlaceKind.Marker, "Market Board", "La Noscea > Limsa Lominsa > Shops", 1, 1, new(50)),
        new("m:2", PlaceKind.Marker, "Mélodie", "La Noscea > Limsa Lominsa > NPC", 1, 1, new(20)),
        new("m:3", PlaceKind.Marker, "Delivery Moogle", "La Noscea > Limsa Lominsa > Service", 1, 1, new(70)),
        new("f:1", PlaceKind.Flag, "Limsa meeting point", "Flag history > Limsa Lominsa", 1, 1, new(-70)),
    ];
    public PaletteImage Image(PlaceEntry place) => new(0, 0, 0, place.MapId * 17 + place.Expansion * 31 + 1);
    public IEnumerable<PlaceEntry> Entries => Places.Concat(driver.Context.Assistance?.PinLayer?.Store.Entries ?? []);
    public int Revision => 1 + (driver.Context.Assistance?.PinLayer?.Store.Revision ?? 0);
    public bool Building => false;
    public PlaceSearchContext Context => new(driver.MapId, 1, driver.PlayerPosition);
    public PlaceEntry? Shown { get; private set; }
    public int Teleports { get; private set; }
    public PlaceAccess Access(PlaceEntry place) =>
        new(place.AetheryteId is 1 or 3 or 4 or 7 or 10, place.AetheryteId == 7 ? 1200u : 320u);
    public void Refresh() {
    }

    public void Used() => driver.Config.PaletteHintSeen = true;
    public void ShowMainStory() => driver.Context.Story.Show(driver.Renderer, Entries, Context);
    public void Show(PlaceEntry place) {
        Shown = place;
        if (place.World is { } world)
            driver.Renderer.BeginSpotlight(new(world, place.MapId,
                place.Kind == PlaceKind.Pin ? SpotlightSource.Pin : SpotlightSource.Search));
    }

    public string Teleport(PlaceEntry place, bool confirmed) {
        if (!Access(place).Attuned || place.IsShard || !driver.Config.PaletteTeleportButton)
            return "Unavailable";
        if (Access(place).Cost >= 1000 && !confirmed)
            return "Confirm 1,200 gil";
        Teleports++;
        return "Teleport requested.";
    }
}