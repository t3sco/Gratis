using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Gratis.Rendering;
using Gratis.Compat.CommandManager;

namespace Gratis.Classes;

internal sealed class GlassDiagnosticCommand : global::System.IDisposable
{
    [PluginService] private IChatGui ChatGui { get; set; } = null!;
    private readonly CommandHandler command;

    public GlassDiagnosticCommand()
    {
        Service.PluginInterface.Inject(this);
        command = new CommandHandler {
            ActivationPath = "/glass",
            Delegate = _ => {
                var config = System.SystemConfig;
                ChatGui.Print($"[Gratis] style={(config.IsGratis ? "Gratis" : "Classic")} map={(System.MapWindow.IsOpen ? "open" : "closed")} " +
                    $"windowBlur={config.BlurBackground}({config.BlurStrength:0.##}); {MapDrawContext.Current.Blur.Status(config)}");
            },
        };
        System.CommandManager.RegisterCommand(command);
    }

    public void Dispose() => System.CommandManager.RemoveCommand(command);
}
