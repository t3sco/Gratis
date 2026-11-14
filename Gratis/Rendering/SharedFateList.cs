using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gratis.Rendering;

/// <summary>One zone card of the game's Shared FATE window, as sampled. <see cref="Progress"/> of <see cref="Needed"/>
/// FATEs count toward the next rank; at <see cref="MaxRank"/> there is nothing left to earn.</summary>
public sealed record SharedFateZone(uint TerritoryId, int Order, string Name, int Rank, int MaxRank, int Progress,
    int Needed);

/// <summary>The three tabs of zones plus the Bicolor Gemstone total. A tab with no zones has not been loaded by the
/// game yet (the game asks the server for each tab when its window shows it).</summary>
public sealed record SharedFateSnapshot(int Revision, IReadOnlyList<SharedFateZone>[] Tabs, int ActiveTab = 0,
    int Gemstones = -1, int GemstoneMax = 0) {
    public static readonly SharedFateSnapshot Empty = new(0, [[], [], []]);

    public IReadOnlyList<SharedFateZone> Zones(int tab) =>
        (uint)tab < (uint)Tabs.Length ? Tabs[tab] : [];

    public bool IsLoaded(int tab) {
        var zones = Zones(tab);
        for (var index = 0; index < zones.Count; index++)
            if (SharedFateList.IsListed(zones[index]))
                return true;
        return false;
    }

    public bool AnyLoaded => IsLoaded(0) || IsLoaded(1) || IsLoaded(2);
}

public interface ISharedFateSource {
    /// <summary>Sampled at most once a second and only read while the panel is open.</summary>
    SharedFateSnapshot Snapshot { get; }

    /// <summary>Empty when the data can be read; otherwise why the panel cannot show it.</summary>
    string Unavailable { get; }
}

/// <summary>Pure rules for the Shared FATE panel, kept apart from drawing so they can be tested without a frame.</summary>
public static class SharedFateList {
    public const int TabCount = 3;
    public const string PvpReason = "Shared FATEs are not available in PvP.";
    public const string NoRoomReason = FateList.NoRoomReason;
    public const string NotReadyReason = "Shared FATE data is not ready.";
    public const string UnloadedText = "Open the Shared FATE window once in game to load it.";
    public const string TabUnloadedText = "Open this tab in the Shared FATE window once in game to load it.";
    public const string Caption = "Shows what the game last loaded.";

    // The game's window offers 1 2 3; cached so the draw path builds no strings.
    private static readonly string[] tabLabels = ["Tab 1", "Tab 2", "Tab 3"];

    /// <summary>Pips are drawn for up to this many needed FATEs; more than that reads better as one bar.</summary>
    public const int MaxPips = 12;

    public static string TabLabel(int tab) => (uint)tab < (uint)tabLabels.Length ? tabLabels[tab] : "";

    /// <summary>Slots the game leaves blank (no territory, no name) are omitted; the rest keep the game's own order.</summary>
    public static bool IsListed(SharedFateZone zone) => zone.TerritoryId != 0 || zone.Name.Length > 0;

    public static SharedFateZone[] Order(IEnumerable<SharedFateZone> zones) => zones
        .Where(IsListed)
        .OrderBy(z => z.Order)
        .ThenBy(z => z.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(z => z.TerritoryId)
        .ToArray();

    public static bool IsMaxRank(SharedFateZone zone) => zone.MaxRank > 0 && zone.Rank >= zone.MaxRank;

    public static string RankLabel(SharedFateZone zone) => "Rank " + Math.Max(0, zone.Rank).ToString(CultureInfo.InvariantCulture);

    public const string MaxRankLabel = "Max rank";

    /// <summary>The chip on the name row: the rank, or the max-rank state.</summary>
    public static string Chip(SharedFateZone zone) => IsMaxRank(zone) ? MaxRankLabel : RankLabel(zone);

    /// <summary>"4/6" toward the next rank. Empty at max rank (nothing left to earn) or when the game gave no target.</summary>
    public static string ProgressText(SharedFateZone zone) => IsMaxRank(zone) || zone.Needed <= 0 ? ""
        : Filled(zone).ToString(CultureInfo.InvariantCulture) + "/" + zone.Needed.ToString(CultureInfo.InvariantCulture);

    /// <summary>FATEs counted so far, never beyond what is needed.</summary>
    public static int Filled(SharedFateZone zone) => Math.Clamp(zone.Progress, 0, Math.Max(0, zone.Needed));

    public static float Fraction(SharedFateZone zone) => IsMaxRank(zone) ? 1
        : zone.Needed <= 0 ? 0 : Filled(zone) / (float)zone.Needed;

    /// <summary>How many pips to draw: one per needed FATE, or 0 for a plain bar (max rank, no target, or too many).</summary>
    public static int Pips(SharedFateZone zone) => !IsMaxRank(zone) && zone.Needed is > 0 and <= MaxPips ? zone.Needed : 0;

    /// <summary>"182 / 1,500", or "182" when the cap is unknown; empty when the total could not be read.</summary>
    public static string GemText(int count, int max) => count < 0 ? ""
        : max > 0 ? count.ToString("N0", CultureInfo.InvariantCulture) + " / " + max.ToString("N0", CultureInfo.InvariantCulture)
        : count.ToString("N0", CultureInfo.InvariantCulture);

    public static float GemFraction(int count, int max) => count < 0 || max <= 0 ? 0 : Math.Clamp(count / (float)max, 0, 1);

    /// <summary>The tab to show first: the game's own when it has data, else the first that does, else the first.</summary>
    public static int DefaultTab(SharedFateSnapshot snapshot) {
        if ((uint)snapshot.ActiveTab < TabCount && snapshot.IsLoaded(snapshot.ActiveTab))
            return snapshot.ActiveTab;
        for (var tab = 0; tab < TabCount; tab++)
            if (snapshot.IsLoaded(tab))
                return tab;
        return 0;
    }

    /// <summary>What to say when the shown tab has no cards.</summary>
    public static string EmptyText(SharedFateSnapshot snapshot) => snapshot.AnyLoaded ? TabUnloadedText : UnloadedText;

    /// <summary>The reason the panel cannot show data right now, or empty when it can.</summary>
    public static string Reason(bool pvp, ISharedFateSource? source) => pvp ? PvpReason : source?.Unavailable ?? "";
}
