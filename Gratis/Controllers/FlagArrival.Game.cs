using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Rendering;
using KamiLib.CommandManager;
using Lumina.Excel.Sheets;

namespace Gratis.Controllers;

internal sealed unsafe class FlagArrivalGame : IFlagArrivalSource, IDisposable
{
    [PluginService] private IChatGui Chat { get; set; } = null!;
    public long Revision { get; private set; }
    private long zoneRevision;
    public FlagArrivalGame()
    {
        Service.PluginInterface.Inject(this);
        MapDrawContext.Current.FlagArrival.Source = this;
        Service.ClientState.TerritoryChanged += TerritoryChanged;
        Service.ClientState.Login += Login;
        Service.ClientState.Logout += Logout;
        System.CommandManager.RegisterCommand(new CommandHandler { ActivationPath = "/flag", Delegate = _ => {
            var state = MapDrawContext.Current.FlagArrival.State;
            Chat.Print($"[Gratis] flag enabled={System.SystemConfig.FlagArrival} radius={System.SystemConfig.FlagArrivalRadius:0.0}y " +
                $"active={state.Flag} distance={state.Distance:0.0} dwell={state.Dwell:0.00}/1.5s arrived={state.Arrived} " +
                $"decision={state.Decision}; auto-clear unavailable (no verified native clear call); native flag retained");
        }});
    }
    private void TerritoryChanged(uint _) => zoneRevision++;
    private void Login() => zoneRevision++;
    private void Logout(int _, int __) => zoneRevision++;
    public void FlagSet() => Revision++;
    public FlagArrivalSample Read()
    {
        var player = Service.ObjectTable.LocalPlayer;
        if (!Service.ClientState.IsLoggedIn || player == null) return new(0, 0, null, null, "", true, zoneRevision);
        var agent = AgentMap.Instance();
        ArrivalFlag? flag = null;
        if (agent->FlagMarkerCount > 0) {
            ref var marker = ref agent->FlagMapMarkers[0];
            flag = new(marker.TerritoryId, marker.MapId, new(marker.XFloat, marker.YFloat), Revision);
        }
        var place = Service.DataManager.GetExcelSheet<Map>().GetRowOrDefault(agent->CurrentMapId)?.PlaceName.ValueNullable?.Name.ExtractText() ?? "flag";
        var loading = Service.ClientState.IsPvP || Service.Condition[ConditionFlag.BetweenAreas] || Service.Condition[ConditionFlag.BetweenAreas51];
        return new(agent->CurrentTerritoryId, agent->CurrentMapId, new Vector2(player.Position.X, player.Position.Z), flag, place, loading, zoneRevision);
    }
    public void Dispose()
    {
        Service.ClientState.TerritoryChanged -= TerritoryChanged;
        Service.ClientState.Login -= Login;
        Service.ClientState.Logout -= Logout;
        System.CommandManager.RemoveCommand("/flag");
        if (ReferenceEquals(MapDrawContext.Current.FlagArrival.Source, this)) MapDrawContext.Current.FlagArrival.Source = null;
    }
}
