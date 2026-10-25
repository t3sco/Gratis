using System;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Gratis.Controllers;

internal sealed class GameTeleportService {
    private (string Id, uint Cost)? pending;
    public void ClearConfirmation() => pending = null;
    public static bool CanTeleport() => BlockedReason().Length == 0;
    public static unsafe string BlockedReason() {
        if (Service.ClientState.IsPvP)
            return "Teleport unavailable in PvP";
        if (!Service.ClientState.IsLoggedIn || Service.ObjectTable.LocalPlayer == null)
            return "Teleport unavailable";
        if (Service.Condition[ConditionFlag.BetweenAreas] || Service.Condition[ConditionFlag.BetweenAreas51])
            return "Changing areas";
        if (AgentMap.Instance()->IsPlayerMoving)
            return "Stop moving to teleport";
        if (Service.Condition[ConditionFlag.InCombat])
            return "In combat";
        if (Service.ObjectTable.LocalPlayer.IsCasting)
            return "Finish casting to teleport";
        if (Service.ClientState.IsPvP || ActionManager.Instance()->GetActionStatus(ActionType.Action, 5) != 0)
            return "Teleport unavailable here";
        return "";
    }

    public unsafe string Request(PlaceEntry place, bool confirmed, bool enabled = true) {
        if (Service.ClientState.IsPvP) {
            pending = null;
            return "Teleport unavailable in PvP";
        }
        if (!System.SystemConfig.IsGratis || !enabled || place.IsShard || place.Kind != PlaceKind.Aetheryte
            || place.AetheryteId == 0)
            return "This place is display-only.";
        if (!Service.ClientState.IsLoggedIn || !UIState.Instance()->IsAetheryteUnlocked(place.AetheryteId))
            return "Not attuned — show this place on the map.";
        Telepo.Instance()->UpdateAetheryteList();
        var destination = Service.AetheryteList.FirstOrDefault(a => a.AetheryteId == place.AetheryteId
            && a.SubIndex == 0);
        if (destination == null)
            return "Teleport is unavailable for this destination.";
        if (!CanTeleport())
            return "Unable to teleport now. Leave combat or finish your current action.";
        var cost = destination.GilCost;
        if (cost > 0 && cost >= Math.Max(0, System.SystemConfig.TravelConfirmGil)
            && (!confirmed || pending != (place.Id, cost))) {
            pending = (place.Id, cost);
            return $"Confirm {cost:N0} gil to {place.Name}";
        }
        pending = null;
        return Telepo.Instance()->Teleport(place.AetheryteId, 0) ? "Teleport requested."
            : "The game refused the teleport.";
    }
}