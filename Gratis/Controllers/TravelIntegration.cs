using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Gratis.Data;
using Gratis.Rendering;
using KamiLib.CommandManager;

namespace Gratis.Controllers;

internal sealed class TravelPlacesAdapter : ITravelPlaces
{
    private readonly GameTeleportService teleportService = new();
    private PlaceIndexGame Source => System.MapWindow.PlacePalette;
    public IEnumerable<PlaceEntry> Entries => Source.Entries;
    public PlaceSearchContext Context => Source.Context;
    public PlaceAccess Access(PlaceEntry place) => Source.Access(place);
    public void Refresh() => Source.Refresh();
    public string Teleport(PlaceEntry place, bool confirmed) => teleportService.Request(place, confirmed);
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
            () => !Service.ClientState.IsPvP && busy.HasFunction && hop.HasFunction && teleport.HasFunction && abort.HasAction
                ? pi.InstalledPlugins.FirstOrDefault(p => p.InternalName == "Lifestream" && p.IsLoaded)?.Version.ToString() : null,
            () => !Service.ClientState.IsPvP && busy.InvokeFunc(), () => Service.ClientState.IsPvP ? 0 : active.InvokeFunc(),
            id => !Service.ClientState.IsPvP && hop.InvokeFunc(id),
            (id, sub) => !Service.ClientState.IsPvP && teleport.InvokeFunc(id, sub),
            () => { if (!Service.ClientState.IsPvP) abort.InvokeAction(); });
        Ui = new MapAssistance(provider, new TravelPlacesAdapter(), GameTeleportService.CanTeleport, SystemConfig.Save, GameTeleportService.BlockedReason);
        Ui.PinLayer = new PinOverlay(new PinStore(System.SystemConfig, () => {
            SystemConfig.Save(); System.MapWindow.PlacePalette.Invalidate();
        }, id => Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>().GetRowOrDefault(id) != null),
            place => System.MapWindow.PlacePalette.Show(place));
        System.MapWindow.PlacePalette.Pins = () => Ui.PinLayer.Store.Entries;
        Ui.CapturePinKeys = () => {
            foreach (var key in Service.KeyState.GetValidVirtualKeys()) Service.KeyState[key] = false;
        };
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/travel",
            Delegate = _ => Chat.Print($"[Gratis] Lifestream detected={provider.Available} version={provider.Version} " +
                $"busy={provider.IsBusy} last action={provider.LastAction}"),
        });
    }

}
