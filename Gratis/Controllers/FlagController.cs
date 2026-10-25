using System;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Compat.Classes;
using Gratis.Data;

namespace Gratis.Controllers;

public unsafe class FlagController : IDisposable {
    private readonly Hook<AgentMap.Delegates.SetFlagMapMarker>? setFlagMapMarkerHook;
    private readonly FlagArrivalGame arrival = new();

    public FlagController() {
        setFlagMapMarkerHook ??= Service.Hooker.HookFromAddress<AgentMap.Delegates.SetFlagMapMarker>(
            AgentMap.MemberFunctionPointers.SetFlagMapMarker, OnSetFlagMapMarker);

        if (Service.ClientState is { IsPvP: false }) {
            EnableIntegrations();
        }
    }

    public void Dispose() {
        arrival.Dispose();
        setFlagMapMarkerHook?.Dispose();
    }

    public void EnableIntegrations() {
        setFlagMapMarkerHook?.Enable();
    }

    public void DisableIntegrations() {
        setFlagMapMarkerHook?.Disable();
    }

    private void OnSetFlagMapMarker(AgentMap* thisPtr, uint territoryId, uint mapId, float x, float y, uint iconId) =>
        HookSafety.ExecuteSafe(() => {
            var newFlagData = new Flag(territoryId, mapId, x, y, iconId);
            var dataFile = System.FlagConfig;

            if (System.SystemConfig.IsGratis || !dataFile.FlagHistory.Contains(newFlagData)) {
                dataFile.FlagHistory.Remove(newFlagData);
                dataFile.FlagHistory.AddFirst(newFlagData);

                if (dataFile.FlagHistory.Count > dataFile.HistoryLimit) {
                    dataFile.FlagHistory.RemoveLast();
                }

                dataFile.Save();
            }

            setFlagMapMarkerHook!.Original.Invoke(thisPtr, territoryId, mapId, x, y, iconId);
            arrival.FlagSet();
        }, Service.Log, "Exception during OnSetFlagMapMarker");
}