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
            ActivationPath = "/motion",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " + System.MapRenderer.TakeMotionDiagnostics()),
        });
    }
}
