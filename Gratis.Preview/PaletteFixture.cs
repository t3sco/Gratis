using Gratis.Controllers;
using Gratis.Rendering;

namespace Gratis.Preview;

internal sealed class PaletteFixture : IPaletteSource {
    private readonly ScriptedFrameDriver driver;
    private PaletteImage crest;
    public List<PlaceEntry> CrestRequests { get; } = [];
    public bool CrestsReady { get; set; } = true;
    public List<PlaceEntry> AdditionalPlaces { get; } = [];
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
        new("q:1", PlaceKind.Quest, "It's Probably Pirates", "La Noscea > Limsa Lominsa", 1, 1, new(140, -50)) {
            QuestRowId = 66209, QuestBucket = QuestBucket.Scenario
        },
        new("m:1", PlaceKind.Marker, "Market Board", "La Noscea > Limsa Lominsa > Shops", 1, 1, new(50)),
        new("m:2", PlaceKind.Marker, "Mélodie", "La Noscea > Limsa Lominsa > NPC", 1, 1, new(20)),
        new("m:3", PlaceKind.Marker, "Delivery Moogle", "La Noscea > Limsa Lominsa > Service", 1, 1, new(70)),
        new("f:1", PlaceKind.Flag, "Limsa meeting point", "Flag history > Limsa Lominsa", 1, 1, new(-70)),
    ];
    public static readonly PlaceEntry[] QuestPlaces = [
        Quest("hunter", 70371, "A Hunter True", QuestBucket.JobClass, 5, 71021),
        Quest("key", 70182, "A Key to the Past", QuestBucket.SideQuests),
        Quest("annihilation", 68628, "Annihilation", QuestBucket.Chronicles),
        Quest("mountains", 70964, "Beyond the Mountains", QuestBucket.Scenario, 5, 71001),
        Quest("cheese", 69703, "Cutting the Cheese", QuestBucket.SideQuests, 4),
        Quest("gilly", 67649, "Do It for Gilly", QuestBucket.SideQuests, 1),
        Quest("corpse", 67060, "Corpse Groom", QuestBucket.SideQuests),
        Quest("society", 70001, "Society sample", QuestBucket.AlliedSocieties),
        Quest("seasonal", 70002, "Seasonal sample", QuestBucket.Other),
        Quest("missing", 999999, "Missing quest sample", QuestBucket.Other),
        Quest("hunter-second", 70371, "A Hunter True", QuestBucket.JobClass, 5, 71021) with { World = new(80) },
    ];
    private static PlaceEntry Quest(string id, uint rowId, string name, QuestBucket bucket, uint expansion = 0,
        uint icon = 71141) => new("q:" + id, PlaceKind.Quest, name, "Active / tracked quest", expansion switch {
            1 => 7u, 4 => 9u, 5 => 10u, _ => 1u
        }, 1, new(40), Expansion: expansion) { QuestRowId = rowId, QuestBucket = bucket, Icon = icon };

    public PaletteImage Image(PlaceEntry place) => new(0, 0, 0, place.MapId * 17 + place.Expansion * 31 + 1);
    public PaletteImage Crest(PlaceEntry place) {
        CrestRequests.Add(place);
        if (!CrestsReady)
            return default;
        if (crest.Ready)
            return crest;
        const int size = 48;
        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++) {
                var dx = x + .5f - size / 2f;
                var dy = y + .5f - size / 2f;
                var distance = MathF.Sqrt(dx * dx + dy * dy);
                var index = (y * size + x) * 4;
                pixels[index] = 229;
                pixels[index + 1] = 204;
                pixels[index + 2] = 149;
                pixels[index + 3] = (byte)(255 * Math.Clamp(1.5f - MathF.Abs(distance - 18), 0, 1));
            }
        return crest = new(driver.Textures.Add(size, size, pixels), size, size);
    }
    public IEnumerable<PlaceEntry> Entries => Places.Concat(AdditionalPlaces)
        .Concat(driver.Context.Assistance?.PinLayer?.Store.Entries ?? []);
    public int Revision => 1 + AdditionalPlaces.Count + (driver.Context.Assistance?.PinLayer?.Store.Revision ?? 0);
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
