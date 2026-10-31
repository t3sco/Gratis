using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Data;

namespace Gratis.Windows;

public partial class MapWindow {
    Rendering.PaletteOverlay Rendering.IMapWindowHost.Palette => PlacePalette.Overlay;

    protected override void DrawContents() {
        windowFrame.Draw(this);
        PlacePalette.CaptureGameKeys();
        System.JournalController?.ReleasePassiveKeyboardCapture();
    }

    SystemConfig Rendering.IMapWindowHost.Config => System.SystemConfig;

    Rendering.MapRendererFrame Rendering.IMapWindowHost.Renderer => System.MapRenderer.Frame;

    Vector2? Rendering.IMapWindowHost.PlayerPosition => Service.ObjectTable.LocalPlayer is { } player
        ? new Vector2(player.Position.X, player.Position.Z) : null;

    double Rendering.IMapWindowHost.Now => ImGui.GetTime();

    bool Rendering.IMapWindowHost.SettingsOpen => System.ConfigWindow.IsOpen;

    bool Rendering.IMapWindowHost.ShouldFade => ShouldFade();

    unsafe bool Rendering.IMapWindowHost.PlayerMoving => AgentMap.Instance()->IsPlayerMoving;

    bool Rendering.IMapWindowHost.MapFocused => IsFocused;

    string Rendering.IMapWindowHost.Title => System.SystemConfig.IsGratis
        ? compactMapTitle : WindowName;

    Rendering.MapLocation? Rendering.IMapWindowHost.Location => cardLocation;

    void Rendering.IMapWindowHost.UpdateWindow() {
        UpdateTitle();
        UpdateStyle();
        UpdateSizePosition();
    }

    void Rendering.IMapWindowHost.SaveWindowPosition() => SystemConfig.Save();

    void Rendering.IMapWindowHost.DrawSpoilerWarning() => DrawSpoilerWarning();

    void Rendering.IMapWindowHost.DrawToolbar() => mapToolbar.Draw();

    void Rendering.IMapWindowHost.DrawCoordinates(bool hovered, Vector2 offset) =>
        mapCoordinateBar.Draw(hovered, offset);

    void Rendering.IMapWindowHost.DrawContextMenu(Vector2 offset) => mapContextMenu.Draw(offset);
}
