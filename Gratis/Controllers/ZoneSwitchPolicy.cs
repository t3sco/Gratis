namespace Gratis.Controllers;

public readonly record struct ZoneSwitchOptions(bool AutoSwitchOnZoneChange, bool OnlyIfViewingCurrentZone,
    bool SwitchInDuties);

public readonly record struct ZoneSwitchRequest(uint OldTerritoryId, uint NewTerritoryId, uint OldMapId,
    uint SelectedTerritoryId, uint SelectedMapId, long UserActionVersion);

public readonly record struct ZoneSwitchInput(uint TerritoryId, uint CurrentTerritoryId, uint CurrentMapId,
    uint SelectedTerritoryId, uint SelectedMapId, bool PlayerReady, bool Loading, bool InDuty, bool InPvP,
    bool WindowOpen, double UserActionAgeSeconds, long UserActionVersion, bool Applying = false);

public enum ZoneSwitchDecision {
    Wait, Switch, Disabled, BrowsingOtherMap, UserAction, Duty, PvP, Reentrant
}

public static class ZoneSwitchPolicy {
    public static ZoneSwitchDecision Decide(ZoneSwitchOptions options, ZoneSwitchRequest request,
        ZoneSwitchInput input) {
        if (input.Applying)
            return ZoneSwitchDecision.Reentrant;
        if (input.Loading || !input.PlayerReady || input.TerritoryId == 0 || input.CurrentMapId == 0
            || input.TerritoryId != request.NewTerritoryId || input.CurrentTerritoryId != request.NewTerritoryId)
            return ZoneSwitchDecision.Wait;
        if (!options.AutoSwitchOnZoneChange)
            return ZoneSwitchDecision.Disabled;
        if (input.InPvP)
            return ZoneSwitchDecision.PvP;
        if (!options.SwitchInDuties && input.InDuty)
            return ZoneSwitchDecision.Duty;
        if (input.UserActionVersion != request.UserActionVersion || input.UserActionAgeSeconds <= 1
            || (input.SelectedMapId != request.SelectedMapId
                || input.SelectedTerritoryId != request.SelectedTerritoryId)
                && (input.SelectedMapId != input.CurrentMapId || input.SelectedTerritoryId != input.CurrentTerritoryId))
            return ZoneSwitchDecision.UserAction;
        if (options.OnlyIfViewingCurrentZone && request.OldMapId != 0 && request.SelectedMapId != 0
            && (request.SelectedMapId != request.OldMapId
                || request.SelectedTerritoryId != 0 && request.SelectedTerritoryId != request.OldTerritoryId))
            return ZoneSwitchDecision.BrowsingOtherMap;
        return ZoneSwitchDecision.Switch;
    }
}

public sealed class ZoneSwitchAttempt {
    public ZoneSwitchRequest? Pending { get; private set; }
    public bool IsApplying { get; private set; }

    public void Request(ZoneSwitchRequest request) {
        Pending = Pending is { } previous ? previous with { NewTerritoryId = request.NewTerritoryId }
            : request;
    }

    public bool TryBeginApply() {
        if (IsApplying || Pending is null)
            return false;
        IsApplying = true;
        return true;
    }

    public void EndApply() => IsApplying = false;
    public void Complete() => Pending = null;
}