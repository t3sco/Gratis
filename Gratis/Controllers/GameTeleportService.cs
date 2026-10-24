using System;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Gratis.Controllers;

internal sealed class GameTeleportService
{
    private (string Id, uint Cost)? pending;
    public void ClearConfirmation() => pending = null;
    public static unsafe bool CanTeleport() => Service.ClientState.IsLoggedIn && !Service.ClientState.IsPvP &&
        Service.ObjectTable.LocalPlayer != null && !Service.Condition[ConditionFlag.InCombat] &&
        !Service.Condition[ConditionFlag.BetweenAreas] && !Service.Condition[ConditionFlag.BetweenAreas51] &&
        ActionManager.Instance()->GetActionStatus(ActionType.Action, 5) == 0;

    public unsafe string Request(PlaceEntry place, bool confirmed, bool enabled = true)
    {
        if (!System.SystemConfig.IsGratis || !enabled || place.IsShard || place.Kind != PlaceKind.Aetheryte || place.AetheryteId == 0)
            return "This place is display-only.";
        if (!Service.ClientState.IsLoggedIn || !UIState.Instance()->IsAetheryteUnlocked(place.AetheryteId))
            return "Not attuned — show this place on the map.";
        Telepo.Instance()->UpdateAetheryteList();
        var destination = Service.AetheryteList.FirstOrDefault(a => a.AetheryteId == place.AetheryteId && a.SubIndex == 0);
        if (destination == null) return "Teleport is unavailable for this destination.";
        if (!CanTeleport()) return "Unable to teleport now. Leave combat or finish your current action.";
        var cost = destination.GilCost;
        if (cost > 0 && cost >= Math.Max(0, System.SystemConfig.TravelConfirmGil) && (!confirmed || pending != (place.Id, cost))) {
            pending = (place.Id, cost); return $"Confirm {cost:N0} gil to {place.Name}";
        }
        pending = null;
        return Telepo.Instance()->Teleport(place.AetheryteId, 0) ? "Teleport requested." : "The game refused the teleport.";
    }
}
