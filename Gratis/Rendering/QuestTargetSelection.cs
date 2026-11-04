using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Gratis.Controllers;

namespace Gratis.Rendering;

public enum QuestMapAccess { Reachable, Duty, Instanced }

public enum QuestDestinationSource { None, Marker, DutyEntrance, EntryNpc, QuestGiver, InstanceMap, Door }

public sealed record QuestEntryPoint(uint DestinationTerritory, PlaceEntry Location, QuestMapAccess Access);

public sealed record QuestDestination(IReadOnlyList<PlaceEntry> Targets, QuestDestinationSource Source, string Note = "");

// The game puts quest markers on the map of the objective, even when that map is a duty arena or a quest-only
// instance. Show on map must land where the player can walk, so such targets are swapped for the duty's overworld
// entrance, then the quest giver, and otherwise left on the instance's own map with a note.
public static class QuestTargetSelection {
    public const string InstanceMapNote = "The objective is inside an instance; showing its map.";
    public const string ThroughDoor = "This quest is through a door";

    public static QuestDestination Resolve(IReadOnlyList<PlaceEntry> targets, Func<PlaceEntry, QuestMapAccess> access,
        Func<PlaceEntry, PlaceEntry?> entrance, Func<PlaceEntry?> giver, Func<PlaceEntry?>? entryNpc = null,
        Func<PlaceEntry?>? door = null) {
        if (targets.Count == 0)
            return new(targets, QuestDestinationSource.None);
        var reachable = targets.Where(t => access(t) == QuestMapAccess.Reachable).ToArray();
        if (reachable.Length > 0)
            return new(reachable.Length == targets.Count ? targets : reachable, QuestDestinationSource.Marker);
        var entrances = targets.Select(entrance).OfType<PlaceEntry>()
            .DistinctBy(t => (t.MapId, t.World)).ToArray();
        if (entrances.Length > 0)
            return new(entrances, QuestDestinationSource.DutyEntrance, "Showing the entrance to " + (entrances[0].Subtitle.Length > 0 ? entrances[0].Subtitle : "the duty"));
        // Quest-only instances have no marker on the overworld; the NPC who takes the player in is where to go.
        if (entryNpc?.Invoke() is { } entry)
            return new([entry], QuestDestinationSource.EntryNpc,
                EntranceLabel(entry));
        // Some quest-only interiors are entered through an object (a door) rather than an NPC.
        if (door?.Invoke() is { } doorway)
            return new([doorway], QuestDestinationSource.Door, EntranceLabel(doorway));
        if (giver() is { } start)
            return new([start], QuestDestinationSource.QuestGiver,
                "The objective is inside an instance; showing where the quest started.");
        // Nothing outside leads in (a door the game has no marker for), so the objective's own map is the best answer.
        return new(targets, QuestDestinationSource.InstanceMap, InstanceMapNote);
    }

    private static string EntranceLabel(PlaceEntry entry) => "Entrance: "
        + (string.IsNullOrWhiteSpace(entry.Subtitle) ? "instance" : entry.Subtitle);

    // All inputs are snapshots: no sheets, player state or native calls belong in selection.
    // Distances are comparable only within the player's territory. Stable ordering breaks ties,
    // and supplies the first candidate when the player is elsewhere or has no position.
    public static PlaceEntry? ChooseEntry(PlaceEntry objective, IReadOnlyList<QuestEntryPoint> candidates,
        uint playerTerritory = 0, Vector2? playerWorld = null) => candidates
        .Where(c => c.DestinationTerritory == objective.TerritoryId && c.Access == QuestMapAccess.Reachable
            && c.Location.MapId != 0 && c.Location.World is { } at && float.IsFinite(at.X) && float.IsFinite(at.Y))
        .OrderBy(c => c.Location.TerritoryId == playerTerritory ? 0 : 1)
        .ThenBy(c => c.Location.TerritoryId == playerTerritory && playerWorld is { } player
            && float.IsFinite(player.X) && float.IsFinite(player.Y)
                ? Vector2.DistanceSquared(c.Location.World!.Value, player) : 0)
        .Select(c => c.Location).FirstOrDefault();

    // Prefer the open map's target so Show on map does not switch maps when it does not have to.
    public static PlaceEntry? Choose(IReadOnlyList<PlaceEntry> targets, uint selectedMapId) =>
        targets.FirstOrDefault(t => t.World != null && t.MapId == selectedMapId)
        ?? targets.FirstOrDefault(t => t.World != null);

    // TerritoryIntendedUse values; quest-only event areas and duties cannot be walked to.
    public static QuestMapAccess Classify(uint territoryUse) => territoryUse switch {
        3 or 4 or 8 or 10 or 31 or 57 or 58 => QuestMapAccess.Duty,
        15 => QuestMapAccess.Instanced,
        _ => QuestMapAccess.Reachable,
    };
}
