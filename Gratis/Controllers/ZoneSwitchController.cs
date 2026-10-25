using System;
using System.Diagnostics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Classes;
using Gratis.Compat.Extensions;

namespace Gratis.Controllers;

public sealed unsafe class ZoneSwitchController : IDisposable
{
    private const double RetryInterval = .1, ReadyTimeout = 5, SettleSeconds = .2;
    private readonly ZoneSwitchAttempt attempt = new();
    private uint observedTerritory, observedMap, observedSelectedTerritory, observedSelectedMap;
    private uint stableTerritory, stableMap, lastEventTerritory;
    private double nextPoll, stableSince, retryStarted = double.NaN;
    private double lastUserAction = double.NegativeInfinity;
    private long userActionVersion;
    private bool loggedIn, initialPending;
    private int retries;
    private DateTimeOffset? lastTerritoryChanged;
    private uint lastEventOldTerritory;
    private string trigger = "reload", decision = "waiting for login";

    public bool IsApplying => attempt.IsApplying;
    private static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

    public ZoneSwitchController()
    {
        loggedIn = Service.ClientState.IsLoggedIn;
        lastEventTerritory = Service.ClientState.TerritoryType;
        initialPending = true;
        Service.ClientState.TerritoryChanged += OnTerritoryChanged;
        Service.ClientState.Login += OnLogin;
        Service.Framework.Update += OnUpdate;
        ZoneDiagnosticCommand.Register(this);
    }

    public void Dispose()
    {
        Service.Framework.Update -= OnUpdate;
        Service.ClientState.Login -= OnLogin;
        Service.ClientState.TerritoryChanged -= OnTerritoryChanged;
        System.CommandManager.RemoveCommand("/zone");
        System.CommandManager.RemoveCommand("/reload");
    }

    public void Reload()
    {
        System.SystemConfig = Data.SystemConfig.Load();
        Request(Service.ClientState.TerritoryType, "reload");
    }

    public void NotifyUserAction()
    {
        if (IsApplying) return;
        lastUserAction = Now;
        userActionVersion++;
    }

    private void OnLogin()
    {
        loggedIn = true;
        Request(Service.ClientState.TerritoryType, "login");
    }

    private void OnTerritoryChanged(uint territory)
    {
        lastTerritoryChanged = DateTimeOffset.UtcNow;
        lastEventOldTerritory = lastEventTerritory;
        lastEventTerritory = territory;
        Request(territory, "territory");
    }

    private void Request(uint territory, string source)
    {
        if (attempt.Pending is not null) Finish("skipped: superseded", clear: false);
        var input = ReadInput();
        attempt.Request(new ZoneSwitchRequest(observedTerritory, territory, observedMap,
            observedSelectedTerritory == 0 ? input.SelectedTerritoryId : observedSelectedTerritory,
            observedSelectedMap == 0 ? input.SelectedMapId : observedSelectedMap, userActionVersion));
        trigger = source;
        decision = "pending: waiting for player/current map";
        retries = 0;
        retryStarted = double.NaN;
        stableSince = Now;
        stableTerritory = stableMap = 0;
        nextPoll = Now + RetryInterval;
    }

    private ZoneSwitchInput ReadInput()
    {
        var agent = AgentMap.Instance();
        return new ZoneSwitchInput(Service.ClientState.TerritoryType,
            agent == null ? 0 : agent->CurrentTerritoryId, agent == null ? 0 : agent->CurrentMapId,
            agent == null ? 0 : agent->SelectedTerritoryId, agent == null ? 0 : agent->SelectedMapId,
            Service.ClientState.IsLoggedIn && Service.ObjectTable.LocalPlayer is not null,
            Service.Condition.IsBetweenAreas(), Service.Condition.IsBoundByDuty(), Service.ClientState.IsPvP,
            System.MapWindow.IsOpen, Now - lastUserAction, userActionVersion, IsApplying);
    }

    private static bool IsReady(ZoneSwitchInput input) => input.PlayerReady && !input.Loading &&
        input.TerritoryId != 0 && input.CurrentTerritoryId == input.TerritoryId && input.CurrentMapId != 0;

    private void Remember(ZoneSwitchInput input)
    {
        observedTerritory = input.CurrentTerritoryId;
        observedMap = input.CurrentMapId;
        observedSelectedTerritory = input.SelectedTerritoryId;
        observedSelectedMap = input.SelectedMapId;
    }

    private void OnUpdate(IFramework framework)
    {
        if (initialPending) {
            initialPending = false;
            var first = ReadInput();
            lastEventTerritory = first.TerritoryId;
            if (IsReady(first)) Remember(first);
            if (loggedIn) Request(first.TerritoryId, "reload");
        }
        var now = Now;
        if (now < nextPoll) return;
        nextPoll = now + RetryInterval;
        if (!Service.ClientState.IsLoggedIn) {
            if (loggedIn && attempt.Pending is not null) Finish("skipped: logout");
            attempt.Complete();
            loggedIn = false;
            userActionVersion = 0;
            lastUserAction = double.NegativeInfinity;
            observedTerritory = observedMap = observedSelectedTerritory = observedSelectedMap = 0;
            return;
        }
        if (!loggedIn) OnLogin();
        var input = ReadInput();
        if (attempt.Pending is { NewTerritoryId: 0 } && input.TerritoryId != 0)
            Request(input.TerritoryId, "login-ready");
        if (attempt.Pending is null && IsReady(input) &&
            (observedTerritory != input.CurrentTerritoryId || observedMap != input.CurrentMapId))
            Request(input.TerritoryId, "current-map");

        if (attempt.Pending is not { } pending) {
            if (IsReady(input)) Remember(input);
            return;
        }
        if (input.Loading) { retryStarted = double.NaN; return; }
        if (double.IsNaN(retryStarted)) retryStarted = now;
        retries++;
        if (stableTerritory != input.CurrentTerritoryId || stableMap != input.CurrentMapId || !IsReady(input)) {
            stableTerritory = input.CurrentTerritoryId;
            stableMap = input.CurrentMapId;
            stableSince = now;
        }
        var options = new ZoneSwitchOptions(System.SystemConfig.AutoSwitchOnZoneChange,
            System.SystemConfig.OnlyIfViewingCurrentZone, System.SystemConfig.SwitchInDuties);
        var result = ZoneSwitchPolicy.Decide(options, pending,
            input with { PlayerReady = input.PlayerReady && now - stableSince >= SettleSeconds });
        if (result == ZoneSwitchDecision.Wait) {
            if (now - retryStarted >= ReadyTimeout) Finish("skipped: player/current map not ready (timeout)");
            return;
        }
        if (result == ZoneSwitchDecision.Switch) {
            if (!attempt.TryBeginApply()) return;
            try {
                var agent = AgentMap.Instance();
                if (agent == null || Service.ObjectTable.LocalPlayer is not { } player) return;
                if (agent->SelectedMapId != input.CurrentMapId || agent->SelectedTerritoryId != input.CurrentTerritoryId) {
                    agent->OpenMapByMapId(input.CurrentMapId, input.CurrentTerritoryId, false);
                    agent->ResetMapMarkers();
                }
                if (agent->SelectedMapId != input.CurrentMapId || agent->SelectedTerritoryId != input.CurrentTerritoryId) {
                    if (now - retryStarted >= ReadyTimeout) Finish("skipped: native selection did not update");
                    return;
                }
                System.SystemConfig.FollowPlayer = true;
                System.MapRenderer.Frame.viewportMotion.Follow.Reset();
                System.MapRenderer.CenterOnGameObject(player);
                if (System.SystemConfig.AutoZoom)
                    MapRenderer.MapRenderer.Scale = DrawHelpers.GetMapScaleFactor() * System.SystemConfig.AutoZoomScaleFactor;
                System.MapWindow.ProcessingCommand = false;
                System.MapWindow.RefreshTitle();
                Finish(input.WindowOpen ? "switched" : "switched: closed (selection only)");
            }
            catch (Exception error) {
                Finish("skipped: native switch failed (" + error.GetType().Name + ")");
            }
            finally { attempt.EndApply(); }
        }
        else Finish("skipped: " + (result switch {
            ZoneSwitchDecision.Disabled => "disabled",
            ZoneSwitchDecision.BrowsingOtherMap => "browsing other map",
            ZoneSwitchDecision.UserAction => "user action",
            ZoneSwitchDecision.Duty => "duties disabled",
            ZoneSwitchDecision.PvP => "PvP",
            _ => "hook recursion guard",
        }));
        if (IsReady(input)) Remember(ReadInput());
    }

    private void Finish(string result, bool clear = true)
    {
        decision = result;
        Service.Log.Information("[Gratis zone] " + Diagnostics());
        if (clear) attempt.Complete();
    }

    public string Diagnostics()
    {
        var input = ReadInput();
        var changed = lastTerritoryChanged?.ToString("HH:mm:ss.fff'Z'") ?? "none";
        return $"TerritoryChanged={changed} {lastEventOldTerritory}->{lastEventTerritory} trigger={trigger} " +
            $"{decision}; retries={retries} current={input.CurrentTerritoryId}/{input.CurrentMapId} " +
            $"selected={input.SelectedTerritoryId}/{input.SelectedMapId} open={input.WindowOpen}";
    }
}
