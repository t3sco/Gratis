#if !GRATIS_PREVIEW
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Compat.CommandManager;
using Lumina.Excel.Sheets;

namespace Gratis.Rendering;

internal sealed unsafe class GameContextMenuActions : IContextMenuActions {
    [PluginService] private IChatGui Chat { get; set; } = null!;
    public GameContextMenuActions() {
        Service.PluginInterface.Inject(this);
        Controllers.CoordinateShareGame.Register();
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/context", Delegate = _ => {
                var ui = MapDrawContext.Current.ContextMenu;
                Chat.Print($"[Gratis] context enabled={System.SystemConfig.GratisContextMenu} "
                    + $"open={ui.IsOpen} capture={ui.CapturesInput} world={ui.World} "
                    + $"marker={ui.Marker?.IconId} last={ui.LastAction}");
            }
        });
    }

    private static AgentMap* Agent => AgentMap.Instance();
    public string MapName =>
        Service.DataManager.GetExcelSheet<Map>().GetRowOrDefault(
            Agent->SelectedMapId)?.PlaceName.ValueNullable?.Name.ExtractText()
        ?? "Map";
    public Vector2 Coordinates(Vector2 world) =>
        CoordinateShare.WorldToMap(world, new Vector2(-Agent->SelectedOffsetX, -Agent->SelectedOffsetY),
            Agent->SelectedMapSizeFactorFloat);
    public string MapLink(Vector2 world) => CoordinateShare.Text(Coordinates(world), MapName);
    public void SetFlag(Vector2 world) {
        Agent->FlagMarkerCount = 0;
        Agent->SetFlagMapMarker(Agent->SelectedTerritoryId, Agent->SelectedMapId, world.X, world.Y);
        var texture = (System.MapRenderer.Frame.WorldToScreen(world, Vector2.Zero) - System.MapRenderer.DrawPosition)
            / MapRenderer.MapRenderer.Scale;
        System.MapRenderer.PingTexturePosition(texture);
    }

    public bool HasFlag => Agent->FlagMarkerCount != 0;
    public void RemoveFlag() => Agent->FlagMarkerCount = 0;

    public IReadOnlyList<ContextMenuItem> RecentFlags => System.FlagConfig.FlagHistory.Take(5).Select(flag => {
        var name = flag.GetMap().PlaceName.ValueNullable?.Name.ExtractText();
        var coordinates = CoordinateShare.WorldToMap(flag.GetCoordinate(),
            new Vector2(flag.GetMap().OffsetX, flag.GetMap().OffsetY), flag.GetMap().SizeFactor / 100f);
        return new ContextMenuItem(name + " · " + CoordinateShare.Text(coordinates), GratisIcons.Flag,
            flag.PlaceFlag);
    }).ToArray();
    public IReadOnlyList<ContextMenuItem> Layers {
        get {
            var current = Service.DataManager.GetExcelSheet<Map>().GetRowOrDefault(Agent->SelectedMapId);
            if (current == null)
                return [];
            var layers = Service.DataManager.GetExcelSheet<Map>().Where(m =>
                m.PlaceName.RowId == current.Value.PlaceName.RowId
                && m.MapIndex != 0).OrderBy(m => m.MapIndex).ToArray();
            if (layers.Length < 2)
                return [];
            return layers.Select(m =>
                new ContextMenuItem(m.PlaceNameSub.ValueNullable?.Name.ExtractText() ?? $"Layer {m.MapIndex}",
                    GratisIcons.Layers, () => {
                        System.IntegrationsController.OpenMap(m.RowId);
                        System.SystemConfig.FollowPlayer = false;
                        System.MapRenderer.DrawOffset = Vector2.Zero;
                    })).ToArray();
        }
    }

    public void CaptureKeys() {
        foreach (var key in Service.KeyState.GetValidVirtualKeys())
            Service.KeyState[key] = false;
    }
}
#endif