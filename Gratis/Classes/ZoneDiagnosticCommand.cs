using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Gratis.Controllers;
using KamiLib.CommandManager;

namespace Gratis.Classes;

internal sealed class ZoneDiagnosticCommand
{
    [PluginService] private IChatGui ChatGui { get; set; } = null!;

    public static void Register(ZoneSwitchController controller)
    {
        var command = new ZoneDiagnosticCommand();
        Service.PluginInterface.Inject(command);
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/zone",
            Delegate = _ => command.ChatGui.Print("[Gratis zone] " + controller.Diagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/reload",
            Delegate = _ => controller.Reload(),
        });
    }
}
