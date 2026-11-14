using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Fates;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Rendering;

namespace Gratis.Controllers;

/// <summary>
/// The FATE panel's data and locate action in the running game. IFateTable wrappers and their formatted text are
/// sampled once a second into a plain snapshot (the same safety rule as the marker progress sampler), never read
/// per frame, and only while the panel asks for them.
/// </summary>
public sealed unsafe class FateGame : IFateListSource {
    private FateListSnapshot snapshot = FateListSnapshot.Empty;
    private long second = long.MinValue;
    private uint territory;
    private int revision;
    private bool failureLogged;

    public string Unavailable {
        get {
            var agent = AgentMap.Instance();
            if (agent == null)
                return "The map is not ready.";
            // FATE positions are in the current zone's coordinates, so another map has nothing to show.
            return agent->SelectedMapId != agent->CurrentMapId ? FateList.OtherMapReason : "";
        }
    }

    public FateListSnapshot Snapshot {
        get {
            Sample();
            return snapshot;
        }
    }

    private void Sample() {
        var now = global::System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var current = Service.ClientState.TerritoryType;
        if (now == second && current == territory)
            return;
        second = now;
        territory = current;
        try {
            var local = Service.ObjectTable.LocalPlayer;
            Vector2? player = local is null ? null : new Vector2(local.Position.X, local.Position.Z);
            var table = Service.FateTable;
            var fates = new List<FateListEntry>(table.Length);
            for (var i = 0; i < table.Length; i++) {
                var fate = table[i];
                if (fate == null)
                    continue;
                // Failed and unrecognised FATEs have nothing left to do, so they are not listed.
                FateListState? state = fate.State switch {
                    FateState.Preparing => FateListState.Preparing,
                    FateState.Running => FateListState.Running,
                    FateState.Ending or FateState.Ended => FateListState.Ending,
                    _ => null,
                };
                if (state is not { } listed)
                    continue;
                var world = new Vector2(fate.Position.X, fate.Position.Z);
                fates.Add(new(fate.FateId, fate.Name.TextValue, fate.Level, listed, fate.Progress, fate.HasBonus,
                    fate.Duration > 0 ? (long)fate.StartTimeEpoch + fate.Duration : 0, world,
                    player is { } at ? Vector2.Distance(at, world) : float.NaN, fate.IconId));
            }
            snapshot = new(++revision, fates, current);
        }
        catch (global::System.Exception error) {
            // Keep the last good list rather than throwing from inside the draw.
            if (!failureLogged) {
                failureLogged = true;
                Service.Log.Warning(error, "[Gratis] Could not sample the FATE table.");
            }
        }
    }

    public bool Locate(FateListEntry fate) {
        if (!System.SystemConfig.IsGratis || Service.ClientState.IsPvP)
            return false;
        var agent = AgentMap.Instance();
        if (agent == null || agent->SelectedMapId != agent->CurrentMapId)
            return false;
        System.ZoneSwitchController?.NotifyUserAction();
        var request = new SpotlightRequest(fate.World, agent->CurrentMapId, SpotlightSource.Search, Icon: fate.IconId,
            IncludePlayer: System.SystemConfig.SpotlightIncludePlayer);
        if (!System.MapRenderer.BeginSpotlight(request)) {
            System.SystemConfig.FollowPlayer = false;
            System.MapRenderer.CenterOnCoordinate(fate.World);
        }
        System.MapWindow.ProcessingCommand = true;
        if (System.SystemConfig.SetFlagOnFateClick)
            PlaceFlag(agent, fate);
        return true;
    }

    /// <summary>Selecting a FATE in the Classic list centres the view on it without a spotlight.</summary>
    public bool Centre(FateListEntry fate) {
        var agent = AgentMap.Instance();
        if (agent == null || agent->SelectedMapId != agent->CurrentMapId)
            return false;
        System.IntegrationsController.OpenOccupiedMap();
        System.SystemConfig.FollowPlayer = false;
        System.MapRenderer.DrawOffset = -fate.World;
        if (System.SystemConfig.SetFlagOnFateClick)
            PlaceFlag(agent, fate);
        return true;
    }

    private static void PlaceFlag(AgentMap* agent, FateListEntry fate) {
        agent->FlagMarkerCount = 0;
        agent->SetFlagMapMarker(agent->CurrentTerritoryId, agent->CurrentMapId, fate.World.X, fate.World.Y);
        AgentChatLog.Instance()->InsertTextCommandParam(1048, false);
    }
}
