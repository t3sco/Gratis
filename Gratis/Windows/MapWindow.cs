using System.Drawing;
using System.Numerics;
using Math = global::System.Math;
using MathF = global::System.MathF;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Classes;
using KamiLib.CommandManager;
using KamiLib.Window;
using Lumina.Excel.Sheets;
using Gratis.Classes;
using Gratis.Classes.MapWindowComponents;
using Gratis.Controllers;
using Gratis.Data;
using Map = Lumina.Excel.Sheets.Map;

namespace Gratis.Windows;

public partial class MapWindow : Window, Rendering.IMapWindowHost
{
    public Vector2 MapDrawOffset => windowFrame.MapDrawOffset;
    public HoverFlags HoveredFlags => windowFrame.HoveredFlags;
    public bool ProcessingCommand { get; set; }

    private readonly Rendering.MapWindowFrame windowFrame = new();
    public PlaceIndexGame PlacePalette { get; } = new();
    Rendering.PaletteOverlay Rendering.IMapWindowHost.Palette => PlacePalette.Overlay;
    public void OpenPalette() { if (System.SystemConfig.IsGratis && System.SystemConfig.Palette) PlacePalette.Overlay.Open(); }
    private uint lastMapId;
    private uint lastAreaPlaceNameId;
    private uint lastSubAreaPlaceNameId;
    private string compactMapTitle = string.Empty;

    private readonly MapToolbar mapToolbar = new();
    private readonly MapCoordinateBar mapCoordinateBar = new();
    private readonly MapContextMenu mapContextMenu = new();

    public MapWindow() : base("###GratisMapWindow", new Vector2(400.0f, 250.0f))
    {
        UpdateTitle();

        DisableWindowSounds = true;
        RegisterCommands();
    }

    public override bool DrawConditions() => IntegrationsController.ShouldShowMap();

    public override unsafe void PreOpenCheck()
    {
        if (System.MapWindow.IsOpen && AgentMap.Instance()->AddonId is 0)
        {
            Service.Log.Debug("[OnShow] MapWindow can not be open now.");
            IsOpen = false;
        }
        
        if (System.SystemConfig.KeepOpen) {
            IsOpen = true;
        }

        if (Service.ClientState is { IsLoggedIn: false } or { IsPvP: true }) IsOpen = false;
    }

    public override void OnOpen()
    {
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

    protected override void DrawContents() { windowFrame.Draw(this); PlacePalette.CaptureGameKeys(); }
    SystemConfig Rendering.IMapWindowHost.Config => System.SystemConfig;
    Rendering.MapRendererFrame Rendering.IMapWindowHost.Renderer => System.MapRenderer.Frame;
    Vector2? Rendering.IMapWindowHost.PlayerPosition => Service.ObjectTable.LocalPlayer is { } player ? new Vector2(player.Position.X, player.Position.Z) : null;
    double Rendering.IMapWindowHost.Now => ImGui.GetTime();
    bool Rendering.IMapWindowHost.SettingsOpen => System.ConfigWindow.IsOpen;
    bool Rendering.IMapWindowHost.ShouldFade => ShouldFade();
    unsafe bool Rendering.IMapWindowHost.PlayerMoving => AgentMap.Instance()->IsPlayerMoving;
    bool Rendering.IMapWindowHost.MapFocused => IsFocused;
    public void WakeGhost() => windowFrame.Ghost.Wake(ImGui.GetTime());
    public string GhostDiagnostics() => windowFrame.Ghost.Diagnostics();
    string Rendering.IMapWindowHost.Title => System.SystemConfig.IsGratis && System.SystemConfig.CompactTitle ? compactMapTitle : WindowName;
    void Rendering.IMapWindowHost.UpdateWindow() { UpdateTitle(); UpdateStyle(); UpdateSizePosition(); }
    void Rendering.IMapWindowHost.SaveWindowPosition() => SystemConfig.Save();
    void Rendering.IMapWindowHost.DrawSpoilerWarning() => DrawSpoilerWarning();
    void Rendering.IMapWindowHost.DrawToolbar() => mapToolbar.Draw();
    void Rendering.IMapWindowHost.DrawCoordinates(bool hovered, Vector2 offset) => mapCoordinateBar.Draw(hovered, offset);
    void Rendering.IMapWindowHost.DrawContextMenu(Vector2 offset) => mapContextMenu.Draw(offset);


    private int pushedGripColors;
    private bool pushedChromeStyles;

    public override void PreDraw()
    {
        windowFrame.PreDraw(this);
        Rendering.MapDrawContext.Current.Blur.BeginFrame(ImGui.GetFrameCount());
        if (System.SystemConfig.LockWindow || System.SystemConfig.IsGratis || System.SystemConfig.ClassicMoveHandle)
            Flags |= ImGuiWindowFlags.NoMove;
        pushedGripColors = 0;
        pushedChromeStyles = System.SystemConfig.IsGratis && System.SystemConfig.NeutralFrame;
        if (pushedChromeStyles) {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, System.SystemConfig.FlatCornerRadius * ImGuiHelpers.GlobalScale);
        }
        if ((IsFocused || HoveredFlags.Any()) && !pushedChromeStyles) return;

        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, Vector4.Zero);
        pushedGripColors = 3;
    }

    public override void PostDraw()
    {
        if (pushedGripColors > 0) ImGui.PopStyleColor(pushedGripColors);
        if (pushedChromeStyles) ImGui.PopStyleVar(3);
        pushedChromeStyles = false;
        pushedGripColors = 0;
    }

    private unsafe void UpdateTitle()
    {
        var mapChanged = lastMapId != AgentMap.Instance()->SelectedMapId;
        var areaChanged = lastAreaPlaceNameId != TerritoryInfo.Instance()->AreaPlaceNameId;
        var subAreaChanged = lastSubAreaPlaceNameId != TerritoryInfo.Instance()->SubAreaPlaceNameId;
        var locationChanged = mapChanged || areaChanged || subAreaChanged;

        if (!locationChanged) return;
        var subLocationString = string.Empty;
        var mapData = Service.DataManager.GetExcelSheet<Map>().GetRow(AgentMap.Instance()->SelectedMapId);
        var compactParts = new global::System.Collections.Generic.List<string>();
        if (System.SystemConfig.ShowRegionLabel) compactParts.Add(mapData.PlaceNameRegion.Value.Name.ExtractText());
        if (System.SystemConfig.ShowMapLabel) compactParts.Add(mapData.PlaceName.Value.Name.ExtractText());
        compactMapTitle = string.Join(" - ", compactParts);

        if (System.SystemConfig.ShowRegionLabel) {
            var mapRegionName = mapData.PlaceNameRegion.Value.Name.ExtractText();
            subLocationString += $" - {mapRegionName}";
        }

        if (System.SystemConfig.ShowMapLabel) {
            var mapName = mapData.PlaceName.Value.Name.ExtractText();
            subLocationString += $" - {mapName}";
        }

        if (AgentMap.Instance()->SelectedMapId == AgentMap.Instance()->CurrentMapId) {
            if (TerritoryInfo.Instance()->AreaPlaceNameId is not 0 && System.SystemConfig.ShowAreaLabel) {
                var areaLabel = Service.DataManager.GetExcelSheet<PlaceName>().GetRow(TerritoryInfo.Instance()->AreaPlaceNameId);
                subLocationString += $" - {areaLabel.Name}";
            }

            if (TerritoryInfo.Instance()->SubAreaPlaceNameId is not 0 && System.SystemConfig.ShowSubAreaLabel) {
                var subAreaLabel = Service.DataManager.GetExcelSheet<PlaceName>().GetRow(TerritoryInfo.Instance()->SubAreaPlaceNameId);
                subLocationString += $" - {subAreaLabel.Name}";
            }
        }

        WindowName = $"{subLocationString}###GratisMapWindow".TrimStart(['-',' ']);

        lastMapId = AgentMap.Instance()->SelectedMapId;
        lastAreaPlaceNameId = TerritoryInfo.Instance()->AreaPlaceNameId;
        lastSubAreaPlaceNameId = TerritoryInfo.Instance()->SubAreaPlaceNameId;
    }

    public void RefreshTitle()
    {
        lastMapId = 0;
        lastAreaPlaceNameId = 0;
        lastSubAreaPlaceNameId = 0;
    }

    private unsafe void UpdateStyle()
    {
        if (System.SystemConfig.UseCustomWindowChrome) {
            Flags |= ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground;
        }
        else {
            Flags &= ~(ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground);
        }

        if (System.SystemConfig.LockWindow) {
            Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;
        }
        else {
            Flags &= ~(ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);
        }

        if (System.SystemConfig.NoFocusOnAppear) {
            Flags |= ImGuiWindowFlags.NoFocusOnAppearing;
        }
        else {
            Flags &= ~ImGuiWindowFlags.NoFocusOnAppearing;
        }

        if (System.SystemConfig.HideWindowBackground || (System.SystemConfig.IsGratis && System.SystemConfig.NeutralFrame)) {
            Flags |= ImGuiWindowFlags.NoBackground;
        }
        else {
            Flags &= ~ImGuiWindowFlags.NoBackground;
        }

        if (!PlacePalette.Overlay.IsOpen && !PlacePalette.Overlay.CapturesInput && Rendering.MapDrawContext.Current.Assistance?.PinLayer?.CapturesInput != true && !Rendering.MapDrawContext.Current.ContextMenu.CapturesInput && Service.KeyState[VirtualKey.ESCAPE] && IsFocused && !IsMapLocked()) {
            AgentMap.Instance()->Hide();
        }


    }

    private void UpdateSizePosition()
    {
        var systemConfig = System.SystemConfig;
        var windowPosition = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();

        if (!IsFocused) {
            if (windowPosition != systemConfig.WindowPosition) {
                ImGui.SetWindowPos(systemConfig.WindowPosition);
            }

            if (windowSize != systemConfig.WindowSize) {
                ImGui.SetWindowSize(systemConfig.WindowSize);
            }
        }
        else {
            if (systemConfig.WindowPosition != windowPosition) {
                systemConfig.WindowPosition = windowPosition;
                SystemConfig.Save();
            }

            if (systemConfig.WindowSize != windowSize) {
                systemConfig.WindowSize = windowSize;
                SystemConfig.Save();
            }
        }
    }

    private static void DrawSpoilerWarning()
    {
        using (ImRaii.PushColor(ImGuiCol.Text, KnownColor.Orange.Vector())) {
            const string warningLine1 = "Warning, Gratis does not protect you from spoilers and will show everything.";
            const string warningLine2 = "Do not use Gratis if you are not comfortable with this.";

            ImGui.SetCursorPos(ImGui.GetContentRegionAvail() / 2.0f - (ImGui.CalcTextSize(warningLine1) * 2.0f) with { X = 0.0f });
            ImGuiHelpers.CenteredText(warningLine1);
            ImGuiHelpers.CenteredText(warningLine2);
        }

        ImGuiHelpers.ScaledDummy(30.0f);
        ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X / 3.0f);
        using (ImRaii.Disabled(!(ImGui.GetIO().KeyShift && ImGui.GetIO().KeyCtrl))) {
            if (ImGui.Button("I understand", new Vector2(ImGui.GetContentRegionAvail().X / 2.0f, 23.0f * ImGuiHelpers.GlobalScale))) {
                System.SystemConfig.AcceptedSpoilerWarning = true;
                SystemConfig.Save();
            }

            using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 1.0f)) {
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
                    ImGui.SetTooltip("Hold Shift + Control while clicking activate button");
                }
            }
        }
    }

    public override unsafe void OnClose()
    {
        windowFrame.MoveHandle.Cancel();
        if (PlacePalette.Overlay.IsOpen) PlacePalette.Overlay.Close(false);
        AgentMap.Instance()->Hide();

        SystemConfig.Save();
    }

    private unsafe bool ShouldFade() =>
        System.SystemConfig.FadeMode.HasFlag(FadeMode.Always) ||
        System.SystemConfig.FadeMode.HasFlag(FadeMode.WhenFocused) && IsFocused ||
        System.SystemConfig.FadeMode.HasFlag(FadeMode.WhenMoving) && AgentMap.Instance()->IsPlayerMoving ||
        System.SystemConfig.FadeMode.HasFlag(FadeMode.WhenUnFocused) && !IsFocused;

    private void RegisterCommands()
    {
        ChromeDiagnosticCommand.Register();
        MotionDiagnosticCommand.Register();
        System.CommandManager.RegisterCommand(new ToggleCommandHandler
        {
            UseShowHideText = true,
            BaseActivationPath = "/map",
            EnableDelegate = _ => System.MapWindow.UnCollapseOrShow(),
            DisableDelegate = _ => System.MapWindow.Close(),
            ToggleDelegate = _ => System.MapWindow.UnCollapseOrToggle(),
        });

        System.CommandManager.RegisterCommand(new CommandHandler
        {
            ActivationPath = "/map/follow",
            Delegate = _ =>
            {
                System.SystemConfig.FollowPlayer = true;
                SystemConfig.Save();
            },
        });

        System.CommandManager.RegisterCommand(new CommandHandler
        {
            ActivationPath = "/map/unfollow",
            Delegate = _ =>
            {
                System.SystemConfig.FollowPlayer = false;
                SystemConfig.Save();
            },
        });

        System.CommandManager.RegisterCommand(new ToggleCommandHandler
        {
            BaseActivationPath = "/autofollow",
            EnableDelegate = _ =>
            {
                System.SystemConfig.FollowOnOpen = true;
                SystemConfig.Save();
            },
            DisableDelegate = _ =>
            {
                System.SystemConfig.FollowOnOpen = false;
                SystemConfig.Save();
            },
            ToggleDelegate = _ =>
            {
                System.SystemConfig.FollowOnOpen = !System.SystemConfig.FollowOnOpen;
                SystemConfig.Save();
            },
        });

        System.CommandManager.RegisterCommand(new ToggleCommandHandler
        {
            BaseActivationPath = "/keepopen",
            EnableDelegate = _ =>
            {
                System.SystemConfig.KeepOpen = true;
                SystemConfig.Save();
            },
            DisableDelegate = _ =>
            {
                System.SystemConfig.KeepOpen = false;
                SystemConfig.Save();
            },
            ToggleDelegate = _ =>
            {
                System.SystemConfig.KeepOpen = !System.SystemConfig.KeepOpen;
                SystemConfig.Save();
            },
        });

        System.CommandManager.RegisterCommand(new CommandHandler
        {
            ActivationPath = "/center/player",
            Delegate = _ =>
            {
                if (Service.ObjectTable.LocalPlayer is { } localPlayer) {
                    System.MapRenderer.CenterOnGameObject(localPlayer);
                }
            },
        });

        System.CommandManager.RegisterCommand(new CommandHandler
        {
            ActivationPath = "/center/map",
            Delegate = _ =>
            {
                System.SystemConfig.FollowPlayer = false;
                System.MapRenderer.DrawOffset = Vector2.Zero;
            },
        });
    }

    private static unsafe bool IsMapLocked()
    {
        var addon = Service.GameGui.GetAddonByName<AddonAreaMap>("AreaMap");
        if (addon is null || addon->RootNode is null) return false;

        return (addon->Param & 0x8_0000) > 0;
    }
}
