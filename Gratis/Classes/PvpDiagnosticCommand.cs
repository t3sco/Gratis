using System;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Gratis.Rendering;
using KamiLib.CommandManager;

namespace Gratis.Classes;

internal sealed class PvpDiagnosticCommand : IDisposable
{
    [PluginService] private IChatGui ChatGui { get; set; } = null!;
    private readonly CommandHandler command;

    public PvpDiagnosticCommand()
    {
        Service.PluginInterface.Inject(this);
        command = new CommandHandler { ActivationPath = "/pvp", Delegate = _ => {
            MapDrawContext.Current.SynchronizePvp();
            ChatGui.Print("[Gratis PvP] " + MapDrawContext.Current.Pvp.Diagnostics());
        } };
        System.CommandManager.RegisterCommand(command);
        Service.ClientState.EnterPvP += OnTransition;
        Service.ClientState.LeavePvP += OnTransition;
        OnTransition();
    }

    private static void OnTransition()
    {
        MapDrawContext.Current.SynchronizePvp();
        System.MapWindow.PlacePalette.ResetPvpState();
    }

    public void Dispose()
    {
        Service.ClientState.EnterPvP -= OnTransition;
        Service.ClientState.LeavePvP -= OnTransition;
        System.CommandManager.RemoveCommand(command);
    }
}
