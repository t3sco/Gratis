using System;
using System.Collections.Generic;
#if !GRATIS_PREVIEW
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Gratis.Rendering;
using Gratis.Windows;
using Gratis.Compat.CommandManager;
#endif

namespace Gratis.Controllers;

[Flags]
public enum JournalMirrorAction {
    None = 0, OpenLog = 1, CloseLog = 2, PushOffscreen = 4, Restore = 8, CloseNative = 16, CloseMap = 32,
}

public sealed class JournalMirrorState {
    public bool Mirroring { get; private set; }
    public bool ClosingNative { get; private set; }
    public bool NativeVisible { get; private set; }
    public bool OpenedMap { get; private set; }
    public bool Fading => fading;
    private bool fading;

    public JournalMirrorAction Show(bool eligible, bool canShow, bool openedMap = false) {
        if (ClosingNative) return JournalMirrorAction.None;
        if (Mirroring && eligible) return JournalMirrorAction.PushOffscreen;
        NativeVisible = true;
        fading = false;
        Mirroring = eligible && canShow;
        OpenedMap = Mirroring && openedMap;
        return Mirroring ? JournalMirrorAction.OpenLog | JournalMirrorAction.PushOffscreen : JournalMirrorAction.Restore;
    }

    public JournalMirrorAction Hide(bool pinned) {
        NativeVisible = false;
        fading |= Mirroring;
        var close = Mirroring && !pinned && !ClosingNative;
        var closeMap = close && OpenedMap;
        OpenedMap = false;
        Mirroring = false;
        return close ? JournalMirrorAction.CloseLog | (closeMap ? JournalMirrorAction.CloseMap : 0)
            : JournalMirrorAction.None;
    }

    public JournalMirrorAction Draw(bool eligible, bool canShow, bool visibleOrFading) {
        if (!eligible) return Disable();
        if (ClosingNative) return JournalMirrorAction.PushOffscreen;
        if (fading) {
            if (visibleOrFading) return JournalMirrorAction.PushOffscreen;
            fading = false;
        }
        if (Mirroring && !canShow)
            return LocalClose() | JournalMirrorAction.CloseLog | JournalMirrorAction.PushOffscreen;
        if (Mirroring) return JournalMirrorAction.PushOffscreen;
        return JournalMirrorAction.Restore;
    }

    // keepMap: the log closed because the user pressed Show on map, so the map it opened must stay.
    public JournalMirrorAction LocalClose(bool keepMap = false) {
        if (!Mirroring || ClosingNative) return JournalMirrorAction.None;
        ClosingNative = true;
        var closeMap = OpenedMap && !keepMap;
        OpenedMap = false;
        return JournalMirrorAction.CloseNative | (closeMap ? JournalMirrorAction.CloseMap : 0);
    }

    public JournalMirrorAction EndClose(bool accepted, bool pinned) {
        if (accepted && NativeVisible) Hide(pinned);
        ClosingNative = false;
        if (!accepted) return Disable();
        return JournalMirrorAction.None;
    }

    public JournalMirrorAction Disable() {
        Mirroring = fading = OpenedMap = false;
        return JournalMirrorAction.Restore;
    }
}

#if !GRATIS_PREVIEW
public sealed unsafe class AddonJournalController : IDisposable {
    public const string AddonName = "Journal";
    private readonly JournalMirrorState state = new();
    private nint shiftedAddon;
    private bool disposed, wasEligible, debug;
    private int mapMissing, updateFrame, missingFrame = -1;
    private readonly Queue<string> journalLog = new();
    private string lastDraw = "", lastUpdate = "", lastAction = "";
    private readonly CommandHandler debugCommand, logCommand;

    private static QuestLogPanel Panel => MapDrawContext.Current.QuestLog;
    private bool Eligible => !disposed && System.SystemConfig.QuestLogReplaceJournal
        && System.SystemConfig.IsGratis && Service.ClientState.IsLoggedIn && !Service.ClientState.IsPvP;

    public AddonJournalController() {
        Service.PluginInterface.Inject(this);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreShow, AddonName, OnShow);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreHide, AddonName, OnHide);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, AddonName, OnDraw);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreShow, "AreaMap", OnMapLifecycle);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreHide, "AreaMap", OnMapLifecycle);
        Service.Framework.Update += Update;
        Panel.Closed += CloseNative;
        debugCommand = new CommandHandler {
            ActivationPath = "/journal/debug",
            Delegate = _ => {
                debug = !debug;
                if (debug) {
                    journalLog.Clear();
                    lastDraw = lastUpdate = lastAction = "";
                }
                Print($"[Gratis journal] debug={debug}");
                Trace("debug", Native());
            },
        };
        logCommand = new CommandHandler {
            ActivationPath = "/journal/log",
            Delegate = _ => {
                foreach (var line in journalLog) Print(line);
            },
        };
        System.CommandManager.RegisterCommand(debugCommand);
        System.CommandManager.RegisterCommand(logCommand);
        Refresh();
    }

    private static AtkUnitBase* Native() => Service.GameGui.GetAddonByName<AtkUnitBase>(AddonName);
    private static bool IsMain(AtkUnitBase* addon) {
        if (addon == null || addon->NameString != AddonName) return false;
        return addon == Native();
    }

    private static bool CanPresent() => System.SystemConfig.IsGratis && Service.ClientState.IsLoggedIn
        && !Service.ClientState.IsPvP && System.MapWindow.IsOpen && AgentMap.Instance() != null
        && AgentMap.Instance()->AddonId != 0 && IntegrationsController.ShouldShowMap();

    private bool OpenLog(out bool openedMap) {
        openedMap = false;
        if (!System.SystemConfig.IsGratis || !IntegrationsController.ShouldShowMap()) return false;
        var map = AgentMap.Instance();
        if (map == null) return false;
        openedMap = !System.MapWindow.IsOpen;
        if (map->AddonId == 0) map->Show();
        if (map->AddonId == 0) return false;
        System.MapWindow.UnCollapseOrShow();
        if (System.MapWindow.PlacePalette.Overlay.IsOpen) System.MapWindow.PlacePalette.Overlay.Close(false);
        System.MapWindow.WakeGhost();
        Panel.OpenFromJournal();
        return true;
    }

    private void OnShow(AddonEvent type, AddonArgs args) {
        var addon = (AtkUnitBase*)args.Addon.Address;
        Trace("PreShow", addon);
        if (!IsMain(addon) || state.ClosingNative) return;
        var eligible = Eligible;
        var opened = false;
        var canShow = eligible && (state.Mirroring || OpenLog(out opened));
        Apply(state.Show(eligible, canShow, opened), addon);
    }

    private void OnHide(AddonEvent type, AddonArgs args) {
        var addon = (AtkUnitBase*)args.Addon.Address;
        Trace("PreHide", addon);
        if (!IsMain(addon)) return;
        Apply(state.Hide(System.SystemConfig.QuestLogPinned), addon);
    }

    private void OnDraw(AddonEvent type, AddonArgs args) {
        var addon = (AtkUnitBase*)args.Addon.Address;
        Trace("PreDraw", addon);
        if (!IsMain(addon)) return;
        Reconcile(addon);
    }

    private void OnMapLifecycle(AddonEvent type, AddonArgs args) => Trace("AreaMap." + type, (AtkUnitBase*)args.Addon.Address);

    private void Reconcile(AtkUnitBase* addon) {
        var visible = addon != null && (addon->IsVisible || addon->RootNode != null && addon->RootNode->Color.A != 0);
        var canPresent = CanPresent();
        if (!state.Mirroring || canPresent) mapMissing = 0;
        else if (missingFrame != updateFrame) {
            missingFrame = updateFrame;
            mapMissing++;
        }
        Apply(state.Draw(Eligible, canPresent || mapMissing <= 5, visible), addon);
    }

    private void Update(IFramework framework) {
        updateFrame++;
        Refresh();
    }

    public void Refresh() {
        var eligible = Eligible;
        if (!eligible && !wasEligible && !debug && shiftedAddon == 0 && !state.Mirroring && !state.Fading
            && !state.ClosingNative && !state.OpenedMap) {
            mapMissing = 0;
            return;
        }
        var addon = Native();
        Trace("Update", addon);
        if (!IsMain(addon)) {
            wasEligible = eligible;
            Apply(state.Disable(), addon);
            return;
        }
        if (eligible && !wasEligible && addon->IsVisible) {
            var canShow = OpenLog(out var opened);
            Apply(state.Show(true, canShow, opened), addon);
        }
        wasEligible = eligible;
        if (state.Mirroring && !addon->IsVisible)
            Apply(state.Hide(System.SystemConfig.QuestLogPinned), addon);
        Reconcile(addon);
    }

    private void Apply(JournalMirrorAction action, AtkUnitBase* addon) {
        if ((action & (JournalMirrorAction.CloseLog | JournalMirrorAction.CloseNative | JournalMirrorAction.CloseMap
            | JournalMirrorAction.Restore)) != 0) Trace("Apply " + action, addon, deduplicate: true);
        if ((action & JournalMirrorAction.CloseLog) != 0) Panel.CloseFromJournal();
        if ((action & JournalMirrorAction.CloseMap) != 0) System.WindowManager.GetWindow<MapWindow>()?.Close();
        if ((action & JournalMirrorAction.Restore) != 0) Restore(addon);
        if ((action & JournalMirrorAction.PushOffscreen) != 0 && addon != null && addon->IsReady && addon->RootNode != null) {
            addon->RootNode->SetPositionFloat(-9001, -9001);
            shiftedAddon = (nint)addon;
        }
        if ((action & JournalMirrorAction.CloseNative) != 0) FinishNativeClose(addon);
    }

    private void Restore(AtkUnitBase* addon) {
        if (addon != null && (nint)addon == shiftedAddon && addon->RootNode != null) {
            addon->RootNode->SetPositionFloat(addon->X, addon->Y);
            shiftedAddon = 0;
        }
        else if (addon == null) shiftedAddon = 0;
    }

    private void CloseNative() {
        Trace("PanelClosed", Native());
        if (!Eligible) {
            Apply(state.Disable(), Native());
            return;
        }
        Apply(state.LocalClose(Panel.KeepMapOnClose), Native());
    }

    private void FinishNativeClose(AtkUnitBase* addon) {
        var accepted = false;
        try {
            if (IsMain(addon)) accepted = addon->Close(false);
        }
        finally {
            Trace("Close accepted=" + accepted, addon);
            Apply(state.EndClose(accepted, System.SystemConfig.QuestLogPinned), Native());
        }
    }

    public void Toggle() {
        if (disposed || !System.SystemConfig.IsGratis || !Service.ClientState.IsLoggedIn || Service.ClientState.IsPvP) return;
        if (System.MapWindow.IsOpen && Panel.IsOpen) Panel.Toggle();
        else OpenLog(out _);
    }

    public void Dispose() {
        disposed = true;
        Panel.Closed -= CloseNative;
        Service.Framework.Update -= Update;
        System.CommandManager.RemoveCommand(debugCommand);
        System.CommandManager.RemoveCommand(logCommand);
        Service.AddonLifecycle.UnregisterListener(OnShow);
        Service.AddonLifecycle.UnregisterListener(OnHide);
        Service.AddonLifecycle.UnregisterListener(OnDraw);
        Service.AddonLifecycle.UnregisterListener(OnMapLifecycle);
        Apply(state.Disable(), Native());
    }

    private void Print(string line) {
        Service.Log.Information(line);
        journalChat.Print(line);
    }

    [Dalamud.IoC.PluginService] private IChatGui journalChat { get; set; } = null!;

    private void Trace(string transition, AtkUnitBase* addon, bool deduplicate = false) {
        if (!debug) return;
        var agent = AgentQuestJournal.Instance();
        var map = AgentMap.Instance();
        var io = ImGui.GetIO();
        var data = $"addon={(addon == null ? "null" : addon->NameString)} id={(addon == null ? 0 : addon->Id)} "
            + $"visible={addon != null && addon->IsVisible} alpha={(addon == null || addon->RootNode == null ? 0 : addon->RootNode->Color.A)} "
            + $"agentId={(agent == null ? 0 : agent->AddonId)} main={IsMain(addon)} shifted={shiftedAddon == (nint)addon && addon != null} "
            + $"mirror={state.Mirroring} fading={state.Fading} closing={state.ClosingNative} nativeVisible={state.NativeVisible} openedMap={state.OpenedMap} "
            + $"replace={System.SystemConfig.QuestLogReplaceJournal} gratis={System.SystemConfig.IsGratis} loggedIn={Service.ClientState.IsLoggedIn} pvp={Service.ClientState.IsPvP} "
            + $"mapOpen={System.MapWindow.IsOpen} mapAgent={map != null} mapId={(map == null ? 0 : map->AddonId)} shouldShow={IntegrationsController.ShouldShowMap()} "
            + $"eligible={Eligible} canPresent={CanPresent()} missing={mapMissing} logOpen={Panel.IsOpen} pinned={System.SystemConfig.QuestLogPinned} "
            + $"WantCaptureKeyboard={io.WantCaptureKeyboard} WantTextInput={io.WantTextInput} panelKeyboard={Panel.CapturesKeyboard} pointer={Panel.PointerOver}";
        ref var last = ref (transition == "PreDraw" ? ref lastDraw
            : ref (transition == "Update" ? ref lastUpdate : ref lastAction));
        if ((transition is "PreDraw" or "Update" || deduplicate) && last == transition + data) return;
        last = transition + data;
        var line = $"[Gratis journal] frame={updateFrame} {transition} {data}";
        if (journalLog.Count == 64) journalLog.Dequeue();
        journalLog.Enqueue(line);
        Service.Log.Information(line);
    }

    public void ReleasePassiveKeyboardCapture() {
        var io = ImGui.GetIO();
        if (!state.Mirroring || !Panel.PointerOver || Panel.CapturesKeyboard || Panel.SearchOpen || io.WantTextInput
            || io.NavVisible || ImGui.IsAnyItemActive() || System.MapWindow.PlacePalette.Overlay.CapturesInput
            || LocationMenuDrawing.CapturesKeyboard || MapDrawContext.Current.ContextMenu.CapturesInput
            || MapDrawContext.Current.Assistance?.PinLayer?.CapturesInput == true
            || ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel)) return;
        ImGui.SetNextFrameWantCaptureKeyboard(false);
    }
}
#endif
