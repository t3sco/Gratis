using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Gratis.Compat.CommandManager;
using Gratis.Controllers;

namespace Gratis.Classes;

internal sealed class AetherDiagnosticCommand {
    [PluginService] private IChatGui ChatGui { get; set; } = null!;

    // /gratis aether: logs how many field aether currents matched a level position, and checks the native unlock
    // calls for the viewed zone. Output also goes to the Dalamud log.
    public static void Register() {
        var command = new AetherDiagnosticCommand();
        Service.PluginInterface.Inject(command);
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/aether",
            Delegate = _ => {
                var text = "[Gratis aether] " + AetherCurrentsGame.Diagnostics();
                Service.Log.Information(text);
                command.ChatGui.Print(text);
            },
        });
    }
}
