using Dalamud.IoC;
using Dalamud.Plugin.Services;
using KamiLib.CommandManager;

namespace Gratis.Classes;

internal sealed class MotionDiagnosticCommand
{
    [PluginService] private IChatGui ChatGui { get; set; } = null!;

    public static void Register()
    {
        var diagnostics = new MotionDiagnosticCommand();
        Service.PluginInterface.Inject(diagnostics);
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/attunement",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " + System.MapWindow.PlacePalette.AttunementDiagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/subzone",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " + System.MapWindow.SubZoneDiagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/measure",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " + Rendering.MapDrawContext.Current.Measure.Diagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/ghost",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " + System.MapWindow.GhostDiagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/view",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " + System.MapRenderer.Frame.ViewMemoryDiagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/pointers",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " + Rendering.MapDrawContext.Current.EdgePointers.Diagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/spotlight",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " + System.MapRenderer.Frame.Spotlight.Diagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/motion",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " + System.MapRenderer.TakeMotionDiagnostics()),
        });
    }
}
