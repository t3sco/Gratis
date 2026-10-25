namespace Gratis.Rendering;

public enum FilterBarMode {
    Collapsed, AlwaysExpanded
}

public sealed class FilterBarSettings {
    public FilterBarMode Mode = FilterBarMode.Collapsed;
    public bool Quests = true, SideQuests = true, Fates = true, Transport = true,
        Links = true, Services = true, Housing = true, GatheringHunts = true,
        Treasure = true, Players = true, Other = true, Pins = true;

    public bool Contains(MarkerGroup group) => group switch {
        MarkerGroup.Quests => Quests,
        MarkerGroup.SideQuests => SideQuests,
        MarkerGroup.Fates => Fates,
        MarkerGroup.Transport => Transport,
        MarkerGroup.Links => Links,
        MarkerGroup.Services => Services,
        MarkerGroup.Housing => Housing,
        MarkerGroup.GatheringHunts => GatheringHunts,
        MarkerGroup.Treasure => Treasure,
        MarkerGroup.Players => Players,
        MarkerGroup.Other => Other,
        MarkerGroup.Pins => Pins,
        _ => false,
    };
}
