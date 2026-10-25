using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Gratis.Controllers;

namespace Gratis.Rendering;

public readonly record struct AetheryteChoice(PlaceEntry Destination, float DistanceToTarget, bool Aethernet);

public static class AetheryteSelection {
    public static bool Useful(uint targetMap, Vector2 target, uint playerMap, Vector2? player, float far = 200) =>
        targetMap != 0
        && Finite(target)
        && (targetMap != playerMap || player == null
            || Finite(player.Value) && Vector2.DistanceSquared(target, player.Value) >= far * far);

    public static AetheryteChoice
        ? Nearest(IEnumerable<PlaceEntry> entries, uint mapId, Vector2 target, Func<PlaceEntry, PlaceAccess> access,
            uint activeAetheryte = 0, bool allowHops = false) {
        if (mapId == 0 || !Finite(target))
            return null;
        var all = entries.Where(p => p.Kind == PlaceKind.Aetheryte && p.AetheryteId != 0).ToArray();
        var active = all.FirstOrDefault(p => p.AetheryteId == activeAetheryte);
        var mainOnMap = all.Any(p => !p.IsShard && p.MapId == mapId && p.World is { } w && Finite(w)
            && access(p).Attuned);
        AetheryteChoice? best = null;
        foreach (var place in all) {
            if (place.MapId != mapId || place.World is not { } world || !Finite(world) || !access(place).Attuned)
                continue;
            var hop = place.IsShard && allowHops && active != null
                && (active.HubId == 0 ? active.AetheryteId : active.HubId) == place.HubId;
            var destination = place;
            if (place.IsShard && !hop) {
                destination = all.FirstOrDefault(p => !p.IsShard && p.AetheryteId == place.HubId && access(p).Attuned);
                if (destination == null || destination.MapId == mapId || mainOnMap)
                    continue;
            }
            var distance = Vector2.Distance(target, world);
            if (best == null || distance < best.Value.DistanceToTarget
                || distance == best.Value.DistanceToTarget
                    && destination.AetheryteId < best.Value.Destination.AetheryteId)
                best = new(destination, distance, hop);
        }
        return best;
    }

    private static bool Finite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
}