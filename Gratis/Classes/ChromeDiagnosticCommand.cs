using Dalamud.Bindings.ImGui;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Gratis.Rendering;
using Gratis.Compat.CommandManager;

namespace Gratis.Classes;

internal sealed class ChromeDiagnosticCommand
{
    [PluginService] private IChatGui ChatGui { get; set; } = null!;

    public static void Register()
    {
        var diagnostics = new ChromeDiagnosticCommand();
        Service.PluginInterface.Inject(diagnostics);
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/chrome",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " +
                MapDrawContext.Current.Chrome.TakeDiagnostics(System.SystemConfig, ImGui.GetTime())),
        });
    }
}
