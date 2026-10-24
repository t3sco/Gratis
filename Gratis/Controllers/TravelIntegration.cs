using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Gratis.Data;
using Gratis.Rendering;
using KamiLib.CommandManager;

namespace Gratis.Controllers;

internal sealed class TravelPlacesAdapter : ITravelPlaces
{
    private PlaceIndexGame Source => System.MapWindow.PlacePalette;
    public IEnumerable<PlaceEntry> Entries => Source.Entries;
    public PlaceSearchContext Context => Source.Context;
    public PlaceAccess Access(PlaceEntry place) => Source.Access(place);
    public void Refresh() => Source.Refresh();
    public string Teleport(PlaceEntry place, bool confirmed) => Source.Teleport(place, confirmed);
    public void Show(PlaceEntry place) => Source.Show(place);
}

internal sealed class TravelIntegration
{
    private static TravelIntegration? current;
    public static MapAssistance Assistance => (current ??= new()).Ui;
    [PluginService] private IChatGui Chat { get; set; } = null!;
    public MapAssistance Ui { get; }

    private TravelIntegration()
    {
        var pi = Service.PluginInterface;
        pi.Inject(this);
        var busy = pi.GetIpcSubscriber<bool>("Lifestream.IsBusy");
        var active = pi.GetIpcSubscriber<uint>("Lifestream.GetActiveAetheryte");
        var hop = pi.GetIpcSubscriber<uint, bool>("Lifestream.AethernetTeleportById");
        var teleport = pi.GetIpcSubscriber<uint, byte, bool>("Lifestream.Teleport");
        var abort = pi.GetIpcSubscriber<object>("Lifestream.Abort");
        var provider = new LifestreamTravelProvider(
            () => busy.HasFunction && hop.HasFunction && teleport.HasFunction && abort.HasAction
                ? pi.InstalledPlugins.FirstOrDefault(p => p.InternalName == "Lifestream" && p.IsLoaded)?.Version.ToString() : null,
            () => busy.InvokeFunc(), () => active.InvokeFunc(), id => hop.InvokeFunc(id),
            (id, sub) => teleport.InvokeFunc(id, sub), () => abort.InvokeAction());
        Ui = new MapAssistance(provider, new TravelPlacesAdapter(), CanTravel, SystemConfig.Save);
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/travel",
            Delegate = _ => Chat.Print($"[Gratis] Lifestream detected={provider.Available} version={provider.Version} " +
                $"busy={provider.IsBusy} last action={provider.LastAction}"),
        });
    }

    private static unsafe bool CanTravel() => Service.ClientState.IsLoggedIn && !Service.ClientState.IsPvP &&
        Service.ObjectTable.LocalPlayer != null && !Service.Condition[ConditionFlag.InCombat] &&
        !Service.Condition[ConditionFlag.BetweenAreas] && !Service.Condition[ConditionFlag.BetweenAreas51] &&
        ActionManager.Instance()->GetActionStatus(ActionType.Action, 5) == 0;
}
