using System;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Compat.Classes;
using Gratis.Compat.Extensions;
using Gratis.Classes;
using MapType = FFXIVClientStructs.FFXIV.Client.UI.Agent.MapType;

namespace Gratis.Controllers;

public unsafe partial class IntegrationsController : IDisposable {
    private readonly Hook<AgentMap.Delegates.ShowMap>? showMapHook;

    private readonly Hook<AgentMap.Delegates.OpenMap>? openMapHook;

    public IntegrationsController() {
        showMapHook ??=
            Service.Hooker.HookFromAddress<AgentMap.Delegates.ShowMap>(AgentMap.MemberFunctionPointers.ShowMap,
                OnShowHook);
        openMapHook ??=
            Service.Hooker.HookFromAddress<AgentMap.Delegates.OpenMap>(AgentMap.MemberFunctionPointers.OpenMap,
                OnOpenMapHook);

        if (Service.ClientState is { IsPvP: false }) {
            EnableIntegrations();
        }

        Service.ClientState.EnterPvP += DisableIntegrations;
        Service.ClientState.LeavePvP += EnableIntegrations;
    }

    public void Dispose() {
        DisableIntegrations();

        showMapHook?.Dispose();
        openMapHook?.Dispose();

        Service.ClientState.EnterPvP -= DisableIntegrations;
        Service.ClientState.LeavePvP -= EnableIntegrations;
    }

    private void EnableIntegrations() {
        Service.Log.Debug("Enabling Integrations");

        showMapHook?.Enable();
        openMapHook?.Enable();

        System.FlagController.EnableIntegrations();
    }

    private void DisableIntegrations() {
        Service.Log.Debug("Disabling Integrations");

        showMapHook?.Disable();
        openMapHook?.Disable();

        System.FlagController.DisableIntegrations();
    }

    private void OnShowHook(AgentMap* agent, bool a1, bool a2) => HookSafety.ExecuteSafe(() => {
        if (System.ZoneSwitchController is { IsApplying: true })
            return;
        Service.Log.Verbose("[OnShow] Beginning Show");

        if (System.MapWindow.IsOpen && AgentMap.Instance()->AddonId is 0) {
            Service.Log.Debug("[OnShow] MapWindow can not be open now.");
            System.MapWindow.Close();
        }

        if (!ShouldShowMap()) {
            Service.Log.Debug("[OnShow] Condition to open map is rejected, aborting.");
            return;
        }

        if (AgentMap.Instance()->AddonId is not 0
            && AgentMap.Instance()->CurrentMapId != AgentMap.Instance()->SelectedMapId) {
            if (!System.SystemConfig.KeepOpen) {
                AgentMap.Instance()->Hide();
            }

            Service.Log.Verbose("[OnShow] Vanilla tried to return to current map, aborted.");
            return;
        }

        if (System.SystemConfig.KeepOpen) {
            Service.Log.Verbose("[OnShow] Keeping Open");
            return;
        }

        showMapHook!.Original(agent, a1, a2);
    }, Service.Log, "Exception during OnShowHook");

    private void OnOpenMapHook(AgentMap* agent, OpenMapInfo* mapInfo) => HookSafety.ExecuteSafe(() => {
        var zoneSwitch = System.ZoneSwitchController;
        if (zoneSwitch is { IsApplying: false } && mapInfo->Type != MapType.Centered)
            zoneSwitch.NotifyUserAction();
        openMapHook!.Original(agent, mapInfo);
        if (zoneSwitch is { IsApplying: true })
            return;

        switch (mapInfo->Type) {
            case MapType.QuestLog:
                ProcessQuestLink(agent, mapInfo);
                break;

            case MapType.GatheringLog:
                ProcessGatheringLink(agent);
                break;

            case MapType.FlagMarker:
                ProcessFlagLink(agent);
                break;

            case MapType.Bozja:
                ProcessForayLink(agent, mapInfo);
                break;

            case MapType.MobHunt:
            case MapType.SharedFate:
            case MapType.Teleport:
            case MapType.Treasure:
                ProcessTeleportLink(agent, mapInfo);
                break;

            case MapType.Centered:

            case MapType.AetherCurrent:
            default:
                Service.Log.Debug($"[OpenMap] Ignoring MapType: {mapInfo->Type}");
                break;
        }

        if (System.SystemConfig.AutoZoom && !System.MapRenderer.Frame.Spotlight.OwnsViewport) {
            MapRenderer.MapRenderer.Scale = DrawHelpers.GetMapScaleFactor()
                * System.SystemConfig.AutoZoomScaleFactor;
        }
    }, Service.Log, "Exception during OpenMap");

    public void OpenMap(uint mapId) {
        System.ZoneSwitchController?.NotifyUserAction();
        AgentMap.Instance()->OpenMapByMapId(mapId, 0, true);

        AgentMap.Instance()->ResetMapMarkers();
    }

    public void OpenOccupiedMap() => OpenMap(AgentMap.Instance()->CurrentMapId);

    public static bool ShouldShowMap() {
        if (Service.ClientState is { IsLoggedIn: false } or { IsPvP: true })
            return false;
        if (System.SystemConfig.HideInCombat && Service.Condition.IsInCombat())
            return false;
        if (System.SystemConfig.HideBetweenAreas && Service.Condition.IsBetweenAreas())
            return false;
        if (System.SystemConfig.HideWithGameGui && !IsNamePlateAddonVisible())
            return false;
        if (System.SystemConfig.HideWithGameGui && Control.Instance()->TargetSystem.TargetModeIndex is 1)
            return false;

        return true;
    }

    private static bool IsNamePlateAddonVisible() =>
        !RaptureAtkUnitManager.Instance()->UiFlags.HasFlag(UiFlags.Nameplates);
}