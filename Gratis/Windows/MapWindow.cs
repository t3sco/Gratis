using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Compat.Extensions;
using Gratis.Compat.Window;
using Gratis.Classes;
using Gratis.Classes.MapWindowComponents;
using Gratis.Controllers;
using Gratis.Data;

namespace Gratis.Windows;

public partial class MapWindow : Window, Rendering.IMapWindowHost {
    public Vector2 MapDrawOffset => windowFrame.MapDrawOffset;

    public HoverFlags HoveredFlags => windowFrame.HoveredFlags;

    public bool ProcessingCommand { get; set; }

    private readonly Rendering.MapWindowFrame windowFrame = new();

    public PlaceIndexGame PlacePalette { get; } = new();
    public QuestLogGame QuestLog { get; } = new();

    public void OpenPalette() {
        if (System.SystemConfig.IsGratis && System.SystemConfig.Palette)
            PlacePalette.Overlay.Open();
    }

    private readonly MapToolbar mapToolbar = new();

    private readonly MapCoordinateBar mapCoordinateBar = new();

    private readonly MapContextMenu mapContextMenu = new();

    public MapWindow() : base("###GratisMapWindow", new Vector2(400.0f, 250.0f)) {
        UpdateTitle();

        DisableWindowSounds = true;
        RegisterCommands();
        Rendering.MapDrawContext.Current.WindowPresets.ShowWindow = UnCollapseOrShow;
    }

    public override bool DrawConditions() => IntegrationsController.ShouldShowMap();

    public override unsafe void PreOpenCheck() {
        if (System.MapWindow.IsOpen && AgentMap.Instance()->AddonId is 0) {
            Service.Log.Debug("[OnShow] MapWindow can not be open now.");
            IsOpen = false;
        }

        if (System.SystemConfig.KeepOpen) {
            IsOpen = true;
        }

        if (Service.ClientState is { IsLoggedIn: false } or { IsPvP: true })
            IsOpen = false;
    }

    public override void OnOpen() {
        Rendering.MapDrawContext.Current.QuestLog.OnOpen(System.SystemConfig);
        Rendering.MapDrawContext.Current.WindowPresets.OnOpen(System.SystemConfig.WindowPresetOptions,
            Service.Condition.IsBoundByDuty());
        WakeGhost();
        System.MapRenderer.Frame.RestoreMapViewOnOpen();
        Rendering.MapDrawContext.Current.Chrome.Reveal(ImGui.GetTime());
        if (ProcessingCommand) {
            ProcessingCommand = false;
            System.SystemConfig.FollowPlayer = false;
            return;
        }

        if (System.SystemConfig.FollowOnOpen) {
            System.IntegrationsController.OpenOccupiedMap();
            System.SystemConfig.FollowPlayer = true;
        }

        switch (System.SystemConfig.CenterOnOpen) {
            case CenterTarget.Player when Service.ObjectTable.LocalPlayer is { } localPlayer:
                System.MapRenderer.CenterOnGameObject(localPlayer);
                break;

            case CenterTarget.Map:
                System.SystemConfig.FollowPlayer = false;
                System.MapRenderer.DrawOffset = Vector2.Zero;
                break;

            case CenterTarget.Disabled:
            default:
                break;
        }
    }

    public void WakeGhost() => windowFrame.Ghost.Wake(ImGui.GetTime());

    public string GhostDiagnostics() => windowFrame.Ghost.Diagnostics();

    public override unsafe void OnClose() {
        Rendering.MapDrawContext.Current.QuestLog.OnClose();
        Rendering.MapDrawContext.Current.WindowPresets.Cancel();
        windowFrame.ResetWindowInput();
        if (PlacePalette.Overlay.IsOpen)
            PlacePalette.Overlay.Close(false);
        AgentMap.Instance()->Hide();

        SystemConfig.Save();
    }

    private unsafe bool ShouldFade() => System.SystemConfig.FadeMode.HasFlag(FadeMode.Always)
        || System.SystemConfig.FadeMode.HasFlag(FadeMode.WhenFocused) && IsFocused
        || System.SystemConfig.FadeMode.HasFlag(FadeMode.WhenMoving) && AgentMap.Instance()->IsPlayerMoving
        || System.SystemConfig.FadeMode.HasFlag(FadeMode.WhenUnFocused) && !IsFocused;

    private static unsafe bool IsMapLocked() {
        var addon = Service.GameGui.GetAddonByName<AddonAreaMap>("AreaMap");
        if (addon is null || addon->RootNode is null)
            return false;

        return (addon->Param & 0x8_0000) > 0;
    }
}