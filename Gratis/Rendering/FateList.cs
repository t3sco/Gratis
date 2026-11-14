using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace Gratis.Rendering;

/// <summary>What a listed FATE is doing. Failed and unknown FATEs are not listed: there is nothing left to act on.</summary>
public enum FateListState { Preparing, Running, Ending }

/// <summary>One FATE as sampled from the game. Time left is derived from <see cref="EndsAt"/> when drawn, so the
/// countdown stays smooth between the once-a-second samples.</summary>
public sealed record FateListEntry(uint Id, string Name, int Level, FateListState State, int Progress, bool Bonus,
    long EndsAt, Vector2 World, float Distance, uint IconId);

public sealed record FateListSnapshot(int Revision, IReadOnlyList<FateListEntry> Fates, uint TerritoryId = 0) {
    public static readonly FateListSnapshot Empty = new(0, []);
}

public interface IFateListSource {
    /// <summary>Sampled at most once a second and only read while the panel is open.</summary>
    FateListSnapshot Snapshot { get; }

    /// <summary>Empty when the viewed map has FATE data; otherwise why the list cannot be shown.</summary>
    string Unavailable { get; }

    /// <summary>Spotlights the FATE on the map. False when the game declined (wrong map, spotlight unavailable).</summary>
    bool Locate(FateListEntry fate);
}

/// <summary>Pure rules for the FATE panel, kept apart from drawing so they can be tested without a frame.</summary>
public static class FateList {
    public const string PvpReason = "FATEs are not available in PvP.";
    public const string OtherMapReason = "FATEs are listed only for the map you are standing in.";
    public const string NoRoomReason = "No room beside the pinned quest log. Unpin it or enlarge the map.";
    public const string EmptyText = "No active FATEs in this zone.";

    // Distances inside this band count as equal, so two FATEs about the same distance away do not swap places every
    // second as the player walks.
    private const float DistanceBand = 10;

    /// <summary>Order the player would want to act in: finished FATEs last, then bonus first (they are the reason to
    /// travel), then running before preparing, then nearest, with name and id as stable tiebreakers.</summary>
    public static FateListEntry[] Sort(IEnumerable<FateListEntry> fates) => fates
        .OrderBy(f => f.State == FateListState.Ending ? 1 : 0)
        .ThenBy(f => f.Bonus ? 0 : 1)
        .ThenBy(f => f.State == FateListState.Running ? 0 : 1)
        .ThenBy(f => float.IsFinite(f.Distance) ? MathF.Floor(f.Distance / DistanceBand) : float.MaxValue)
        .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(f => f.Id)
        .ToArray();

    public static long RemainingSeconds(FateListEntry fate, long now) => fate.EndsAt <= 0 ? -1 : Math.Max(0, fate.EndsAt - now);

    public static string StateLabel(FateListEntry fate) => fate.State switch {
        FateListState.Preparing => "Preparing",
        FateListState.Ending => "Ending",
        _ => "Running",
    };

    /// <summary>The left of the second row: the state, plus progress while it is running.</summary>
    public static string StateText(FateListEntry fate) => fate.State == FateListState.Running
        ? "Running " + Math.Clamp(fate.Progress, 0, 100).ToString(CultureInfo.InvariantCulture) + "%"
        : StateLabel(fate);

    public static string Distance(float yalms) => float.IsFinite(yalms) && yalms >= 0
        ? MathF.Round(yalms, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + " yalms"
        : "";

    /// <summary>The right of the second row: time left (running FATEs only) and distance.</summary>
    public static string Trailing(FateListEntry fate, long now) {
        var time = fate.State == FateListState.Running && RemainingSeconds(fate, now) is >= 0 and var left
            ? FateProgress.FormatTime(left) : "";
        var distance = Distance(fate.Distance);
        return time.Length > 0 && distance.Length > 0 ? time + "  ·  " + distance : time + distance;
    }

    public static float Fraction(FateListEntry fate) => fate.State switch {
        FateListState.Running => Math.Clamp(fate.Progress, 0, 100) / 100f,
        FateListState.Ending => 1,
        _ => 0,
    };

    /// <summary>The reason the panel cannot show the list right now, or empty when it can.</summary>
    public static string Reason(bool pvp, IFateListSource? source) => pvp ? PvpReason : source?.Unavailable ?? "";
}
