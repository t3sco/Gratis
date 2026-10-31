using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Utility;
using Gratis.Compat.Window;
using Gratis.Data;
using Gratis.Rendering;

namespace Gratis.Windows;

public sealed class WhatsNewWindow : Window {
    private const string ChangelogSection = "Changelog";
    private readonly WhatsNewPanel panel = new();
    private SettingsTheme? theme;
    private bool focusOnOpen;

    public WhatsNewWindow() : base("Gratis what's new###GratisWhatsNewWindow",
        new Vector2(WhatsNewPanel.Width, WhatsNewPanel.Height), true) {
        Flags |= WhatsNewPanel.Flags | ImGuiWindowFlags.NoFocusOnAppearing;
        DisableWindowSounds = true;
        PositionCondition = ImGuiCond.Appearing;
        panel.Save = SystemConfig.Save;
        panel.OpenChangelog = OpenChangelog;
    }

    public void ShowNow() {
        focusOnOpen = true;
        Open();
    }

    public override void PreOpenCheck() {
        if (!IsOpen && WhatsNew.ShouldShow(System.SystemConfig, Gates()))
            Open();
    }

    public override void OnOpen() {
        panel.Open();
        var viewport = ImGuiHelpers.MainViewport;
        Position = WhatsNewPanel.Centred(viewport.Pos, viewport.Size, ImGuiHelpers.GlobalScale);
        if (!focusOnOpen)
            return;
        focusOnOpen = false;
        ImGui.SetWindowFocus(WindowName);
    }

    public override void PreDraw() {
        theme = SettingsTheme.Push(System.SystemConfig);
        WhatsNewPanel.PushStyle();
    }

    public override void PostDraw() {
        WhatsNewPanel.PopStyle();
        theme?.Pop();
        theme = null;
    }

    protected override void DrawContents() {
        if (!panel.Draw(System.SystemConfig))
            Close();
    }

    private static void OpenChangelog() {
        System.ConfigWindow.OpenSection(ChangelogSection);
        System.ConfigWindow.UnCollapseOrShow();
    }

    private static WhatsNewGates Gates() {
        var condition = Service.Condition;
        return new(Service.ClientState.IsLoggedIn,
            condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51],
            condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56]
                || condition[ConditionFlag.BoundByDuty95],
            condition[ConditionFlag.WatchingCutscene] || condition[ConditionFlag.WatchingCutscene78]
                || condition[ConditionFlag.OccupiedInCutSceneEvent],
            condition[ConditionFlag.InCombat], Service.ClientState.IsPvP,
            MapDrawContext.Current.QuickSetup.IsOpen);
    }
}
