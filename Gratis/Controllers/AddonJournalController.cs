using System;
#if !GRATIS_PREVIEW
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Gratis.Rendering;
#endif

namespace Gratis.Controllers;

[Flags]
public enum JournalMirrorAction {
    None = 0, OpenLog = 1, CloseLog = 2, PushOffscreen = 4, Restore = 8, CloseNative = 16,
}

public sealed class JournalMirrorState {
    public bool Mirroring { get; private set; }
    public bool ClosingNative { get; private set; }
    public bool NativeVisible { get; private set; }
    private bool fading;

    public JournalMirrorAction Show(bool eligible, bool canShow) {
        if (ClosingNative) return JournalMirrorAction.None;
        NativeVisible = true;
        fading = false;
        Mirroring = eligible && canShow;
        return Mirroring ? JournalMirrorAction.OpenLog | JournalMirrorAction.PushOffscreen : JournalMirrorAction.Restore;
    }

    public JournalMirrorAction Hide(bool pinned) {
        NativeVisible = false;
        fading |= Mirroring;
        var close = Mirroring && !pinned && !ClosingNative;
        Mirroring = false;
        return close ? JournalMirrorAction.CloseLog : JournalMirrorAction.None;
    }

    public JournalMirrorAction Draw(bool eligible, bool canShow, bool visibleOrFading) {
        if (!eligible) return Disable();
        if (fading) {
            if (visibleOrFading) return JournalMirrorAction.PushOffscreen;
            fading = false;
        }
        if (!canShow) return Disable();
        if (Mirroring) return JournalMirrorAction.PushOffscreen;
        return JournalMirrorAction.Restore;
    }

    public JournalMirrorAction LocalClose() {
        if (!Mirroring || ClosingNative) return JournalMirrorAction.None;
        ClosingNative = true;
        return JournalMirrorAction.CloseNative;
    }

    public JournalMirrorAction EndClose(bool accepted, bool pinned) {
        if (accepted && NativeVisible) Hide(pinned);
        ClosingNative = false;
        if (!accepted) return Disable();
        return JournalMirrorAction.None;
    }

    public JournalMirrorAction Disable() {
        Mirroring = fading = false;
        return JournalMirrorAction.Restore;
    }
}

#if !GRATIS_PREVIEW
public sealed unsafe class AddonJournalController : IDisposable {
    public const string AddonName = "Journal";
    private readonly JournalMirrorState state = new();
    private nint shiftedAddon;
    private bool disposed, wasEligible;

    private static QuestLogPanel Panel => MapDrawContext.Current.QuestLog;
    private bool Eligible => !disposed && System.SystemConfig.QuestLogReplaceJournal
        && System.SystemConfig.IsGratis && Service.ClientState.IsLoggedIn && !Service.ClientState.IsPvP;

    public AddonJournalController() {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreShow, AddonName, OnShow);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreHide, AddonName, OnHide);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, AddonName, OnDraw);
        Service.Framework.Update += Update;
        Panel.Closed += CloseNative;
        Refresh();
    }

    private static AtkUnitBase* Native() => Service.GameGui.GetAddonByName<AtkUnitBase>(AddonName);
    private static bool IsMain(AtkUnitBase* addon) {
        if (addon == null || addon->NameString != AddonName) return false;
        var agent = AgentQuestJournal.Instance();
        return agent != null && (agent->AddonId == 0 || agent->AddonId == addon->Id);
    }

    private static bool CanPresent() => System.SystemConfig.IsGratis && Service.ClientState.IsLoggedIn
        && !Service.ClientState.IsPvP && System.MapWindow.IsOpen && AgentMap.Instance() != null
        && AgentMap.Instance()->AddonId != 0 && IntegrationsController.ShouldShowMap();

    private static bool OpenLog() {
        if (!System.SystemConfig.IsGratis || !IntegrationsController.ShouldShowMap()) return false;
        var map = AgentMap.Instance();
        if (map == null) return false;
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
        if (!IsMain(addon) || state.ClosingNative) return;
        var eligible = Eligible;
        Apply(state.Show(eligible, eligible && OpenLog()), addon);
    }

    private void OnHide(AddonEvent type, AddonArgs args) {
        var addon = (AtkUnitBase*)args.Addon.Address;
        Apply(state.Hide(System.SystemConfig.QuestLogPinned), addon);
    }

    private void OnDraw(AddonEvent type, AddonArgs args) {
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (!IsMain(addon)) { Restore(addon); return; }
        Reconcile(addon);
    }

    private void Reconcile(AtkUnitBase* addon) {
        var visible = addon != null && (addon->IsVisible || addon->RootNode != null && addon->RootNode->Color.A != 0);
        Apply(state.Draw(Eligible, CanPresent(), visible), addon);
    }

    private void Update(IFramework framework) => Refresh();

    public void Refresh() {
        var eligible = Eligible;
        var addon = Native();
        if (!IsMain(addon)) {
            wasEligible = eligible;
            Apply(state.Disable(), addon);
            return;
        }
        if (eligible && !wasEligible && IsMain(addon) && addon->IsVisible)
            Apply(state.Show(true, OpenLog()), addon);
        wasEligible = eligible;
        Reconcile(addon);
    }

    private void Apply(JournalMirrorAction action, AtkUnitBase* addon) {
        if ((action & JournalMirrorAction.CloseLog) != 0) Panel.CloseFromJournal();
        if ((action & JournalMirrorAction.Restore) != 0) Restore(addon);
        if ((action & JournalMirrorAction.PushOffscreen) != 0 && addon != null && addon->IsReady && addon->RootNode != null) {
            addon->RootNode->SetPositionFloat(-9001, -9001);
            shiftedAddon = (nint)addon;
        }
    }

    private void Restore(AtkUnitBase* addon) {
        if (addon != null && (nint)addon == shiftedAddon && addon->RootNode != null) {
            addon->RootNode->SetPositionFloat(addon->X, addon->Y);
            shiftedAddon = 0;
        }
        else if (addon == null) shiftedAddon = 0;
    }

    private void CloseNative() {
        if (!Eligible) {
            Apply(state.Disable(), Native());
            return;
        }
        if ((state.LocalClose() & JournalMirrorAction.CloseNative) == 0) return;
        var addon = Native();
        var accepted = false;
        try {
            if (IsMain(addon)) accepted = addon->Close(false);
        }
        finally { Apply(state.EndClose(accepted, System.SystemConfig.QuestLogPinned), Native()); }
    }

    public void Toggle() {
        if (disposed || !System.SystemConfig.IsGratis || !Service.ClientState.IsLoggedIn || Service.ClientState.IsPvP) return;
        if (System.MapWindow.IsOpen && Panel.IsOpen) Panel.Toggle();
        else OpenLog();
    }

    public void Dispose() {
        disposed = true;
        Panel.Closed -= CloseNative;
        Service.Framework.Update -= Update;
        Service.AddonLifecycle.UnregisterListener(OnShow);
        Service.AddonLifecycle.UnregisterListener(OnHide);
        Service.AddonLifecycle.UnregisterListener(OnDraw);
        Apply(state.Disable(), Native());
    }
}
#endif
