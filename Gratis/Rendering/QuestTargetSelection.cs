using System;
using System.Collections.Generic;
using System.Linq;
using Gratis.Controllers;

namespace Gratis.Rendering;

public enum QuestMapAccess { Reachable, Duty, Instanced }

public enum QuestDestinationSource { None, Marker, DutyEntrance, QuestGiver }

public sealed record QuestDestination(IReadOnlyList<PlaceEntry> Targets, QuestDestinationSource Source, string Note = "");

// The game puts quest markers on the map of the objective, even when that map is a duty arena or a quest-only
// instance. Show on map must land where the player can walk, so such targets are swapped for the duty's overworld
// entrance, then the quest giver, and otherwise reported as having no destination.
public static class QuestTargetSelection {
    public const string InstanceOnly = "Instance only: no map destination";

    public static QuestDestination Resolve(IReadOnlyList<PlaceEntry> targets, Func<PlaceEntry, QuestMapAccess> access,
        Func<PlaceEntry, PlaceEntry?> entrance, Func<PlaceEntry?> giver) {
        if (targets.Count == 0)
            return new(targets, QuestDestinationSource.None);
        var reachable = targets.Where(t => access(t) == QuestMapAccess.Reachable).ToArray();
        if (reachable.Length > 0)
            return new(reachable.Length == targets.Count ? targets : reachable, QuestDestinationSource.Marker);
        var entrances = targets.Select(entrance).OfType<PlaceEntry>()
            .DistinctBy(t => (t.MapId, t.World)).ToArray();
        if (entrances.Length > 0)
            return new(entrances, QuestDestinationSource.DutyEntrance, "Showing the entrance to " + (entrances[0].Subtitle.Length > 0 ? entrances[0].Subtitle : "the duty"));
        if (giver() is { } start)
            return new([start], QuestDestinationSource.QuestGiver,
                "The objective is inside an instance; showing where the quest started.");
        return new([], QuestDestinationSource.None, InstanceOnly);
    }

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
