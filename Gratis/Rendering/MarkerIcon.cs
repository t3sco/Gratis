using Gratis.Classes;

namespace Gratis.Rendering;

public enum MarkerIconFamily {
    Auto, Landmark, Aetheryte, Aethernet, Quest, Fate, FateBoss, FateCollect, FateDefend,
    FateEscort, FateSpecial, Duty, Exit, Shop, Mender, Inn, Housing, Stable, Mail,
    Bell, Market, Gathering, Botany, Fishing, Hunt, Treasure, Flag, Party, Alliance,
    Player, Pet, Anchor, Chocobo, AetherCurrent, Leve, Lore, Guildhest, Cards, Gate, Novice, Neutral,
}

public enum QuestMarkerState {
    Available, InProgress, Complete, Locked
}

public readonly record struct QuestMarker(QuestMarkerState State, bool MainScenario, bool Feature, bool Repeatable);

public static class MarkerIcon {
    public static bool TryQuest(uint id, out QuestMarker quest) {
        var block = id / 10;
        var main = block is 7100 or 7101 or 7120 or 7121;
        var feature = block is 7114 or 7115 or 7134 or 7135;
        var side = block is 7102 or 7103 or 7122 or 7123;
        var digit = id % 10;
        if (!(main || feature || side) || digit is < 1 or > 6) {
            quest = default;
            return false;
        }
        var locked = block % 2 == 1;
        quest = new QuestMarker(locked ? QuestMarkerState.Locked : digit == 5 ? QuestMarkerState.Complete
            : digit is 3 or 4 or 6 ? QuestMarkerState.InProgress : QuestMarkerState.Available,
            main, feature, digit == 2);
        return true;
    }

    public static MarkerIconFamily Family(MarkerInfo marker) {
        if (marker.IconFamily != MarkerIconFamily.Auto)
            return marker.IconFamily;
        if (marker.IsFlag)
            return MarkerIconFamily.Flag;
        var id = marker.IconId;
        if (TryQuest(id, out _))
            return MarkerIconFamily.Quest;
        if (marker.MarkerType == MarkerType.Stellar)
            return MarkerIconFamily.FateSpecial;
        if (marker.MarkerType == MarkerType.Fate)
            return FateFamily(id);
        if (marker.IsObjective)
            return MarkerIconFamily.Quest;
        return id switch {
            60453 or 60959 => MarkerIconFamily.Aetheryte,
            60430 or 60452 => MarkerIconFamily.Aethernet,
            60421 => MarkerIconFamily.Party,
            60403 => MarkerIconFamily.Alliance,
            60444 => MarkerIconFamily.Player,
            60961 => MarkerIconFamily.Pet,
            60071 or 60091 or 60092 or 60094 or 60095 or 60096 or 60098 or 60099 => MarkerIconFamily.Quest,
            60093 => MarkerIconFamily.Fate,
            >= 60501 and <= 60508 or >= 60721 and <= 60728 or >= 63910 and <= 63912 => FateFamily(id),
            60401 or 60402 or 60422 or 60424 => MarkerIconFamily.Neutral,
            >= 63000 and <= 63150 => MarkerIconFamily.Hunt,
            60003 or 60913 => MarkerIconFamily.Treasure,
            60437 or 60438 => MarkerIconFamily.Gathering,
            60432 or 60433 => MarkerIconFamily.Botany,
            60445 or >= 70200 and <= 70299 => MarkerIconFamily.Fishing,
            60653 => MarkerIconFamily.AetherCurrent,
            60442 => MarkerIconFamily.Flag,
            >= 61240 and <= 61249 => MarkerIconFamily.Flag,
            60412 or 61731 => MarkerIconFamily.Shop,
            60434 or 60910 => MarkerIconFamily.Mender,
            60436 or 60559 or 60988 => MarkerIconFamily.Inn,
            60425 or 60560 => MarkerIconFamily.Bell,
            60426 => MarkerIconFamily.Mail,
            60427 or 60454 or 60455 or 60751 or 60752 => MarkerIconFamily.Housing,
            60460 or 60570 => MarkerIconFamily.Market,
            60581 => MarkerIconFamily.Stable,
            60414 or 60428 or 60449 or 60459 or 60971 => MarkerIconFamily.Duty,
            60446 or 60447 or 60467 => MarkerIconFamily.Exit,
            60441 => MarkerIconFamily.Chocobo,
            60448 => MarkerIconFamily.Anchor,
            60450 or 60451 or 60473 => MarkerIconFamily.Landmark,
            >= 71040 and <= 71059 or >= 71240 and <= 71259 => MarkerIconFamily.Leve,
            >= 71060 and <= 71079 or >= 71260 and <= 71279 => MarkerIconFamily.Lore,
            >= 71080 and <= 71099 or >= 71280 and <= 71299 => MarkerIconFamily.Guildhest,
            >= 71100 and <= 71109 or >= 71300 and <= 71309 => MarkerIconFamily.Cards,
            >= 71110 and <= 71119 or >= 71310 and <= 71319 => MarkerIconFamily.Gate,
            >= 71120 and <= 71139 or >= 71320 and <= 71339 => MarkerIconFamily.Novice,
            _ when DrawHelpers.IsRegionIcon(id) => MarkerIconFamily.Exit,
            _ => MarkerIconFamily.Neutral,
        };
    }

    private static MarkerIconFamily FateFamily(uint id) => id switch {
        60502 or 60722 => MarkerIconFamily.FateBoss,
        60503 or 60723 => MarkerIconFamily.FateCollect,
        60504 or 60505 or 60724 or 60725 or 63912 => MarkerIconFamily.FateDefend,
        60506 or 60727 => MarkerIconFamily.FateEscort,
        60508 or 60726 or >= 63910 and <= 63911 => MarkerIconFamily.FateSpecial,
        _ => MarkerIconFamily.Fate,
    };

    public static MarkerCategory Category(MarkerIconFamily family) => family switch {
        MarkerIconFamily.Aetheryte or MarkerIconFamily.Aethernet or MarkerIconFamily.Anchor or MarkerIconFamily.Chocobo =>
            MarkerCategory.Transport,
        MarkerIconFamily.Quest or MarkerIconFamily.Fate or MarkerIconFamily.FateBoss or MarkerIconFamily.FateCollect or MarkerIconFamily.FateDefend or MarkerIconFamily.FateEscort or MarkerIconFamily.FateSpecial or MarkerIconFamily.Flag or MarkerIconFamily.Leve =>
            MarkerCategory.Objective,
        MarkerIconFamily.Duty or MarkerIconFamily.Exit => MarkerCategory.Link,
        MarkerIconFamily.Shop or MarkerIconFamily.Mender or MarkerIconFamily.Inn or MarkerIconFamily.Housing or MarkerIconFamily.Stable or MarkerIconFamily.Mail or MarkerIconFamily.Bell or MarkerIconFamily.Market =>
            MarkerCategory.Service,
        MarkerIconFamily.Party or MarkerIconFamily.Alliance or MarkerIconFamily.Player or MarkerIconFamily.Pet =>
            MarkerCategory.Party,
        _ => MarkerCategory.Poi,
    };

    public static char Glyph(MarkerIconFamily family) => family switch {
        MarkerIconFamily.Fate or MarkerIconFamily.Duty or MarkerIconFamily.Guildhest => GratisIcons.Swords,
        MarkerIconFamily.FateBoss => GratisIcons.Skull,
        MarkerIconFamily.FateCollect or MarkerIconFamily.Treasure => GratisIcons.Package,
        MarkerIconFamily.FateDefend or MarkerIconFamily.Novice => GratisIcons.Shield,
        MarkerIconFamily.FateEscort => GratisIcons.Footprints,
        MarkerIconFamily.Exit => GratisIcons.DoorOpen,
        MarkerIconFamily.FateSpecial or MarkerIconFamily.AetherCurrent => GratisIcons.Sparkles,
        MarkerIconFamily.Shop => GratisIcons.Store,
        MarkerIconFamily.Mender => GratisIcons.Wrench,
        MarkerIconFamily.Inn => GratisIcons.Bed,
        MarkerIconFamily.Housing => GratisIcons.House,
        MarkerIconFamily.Stable => GratisIcons.Fence,
        MarkerIconFamily.Chocobo => GratisIcons.Bird,
        MarkerIconFamily.Pet => GratisIcons.PawPrint,
        MarkerIconFamily.Mail => GratisIcons.Mail,
        MarkerIconFamily.Bell => GratisIcons.Bell,
        MarkerIconFamily.Market => GratisIcons.ShoppingBasket,
        MarkerIconFamily.Gathering => GratisIcons.Pickaxe,
        MarkerIconFamily.Botany => GratisIcons.Trees,
        MarkerIconFamily.Fishing => GratisIcons.Fish,
        MarkerIconFamily.Hunt => GratisIcons.Crosshair,
        MarkerIconFamily.Flag => GratisIcons.Flag,
        MarkerIconFamily.Anchor => GratisIcons.Anchor,
        MarkerIconFamily.Leve or MarkerIconFamily.Lore => GratisIcons.BookOpen,
        MarkerIconFamily.Cards => GratisIcons.Layers,
        MarkerIconFamily.Gate => GratisIcons.Ticket,
        _ => GratisIcons.Landmark,
    };
}