using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Gratis.Compat.CommandManager;

namespace Gratis.Classes;

internal sealed class MotionDiagnosticCommand {
    [PluginService] private IChatGui ChatGui { get; set; } = null!;

    public static void Register() {
        var diagnostics = new MotionDiagnosticCommand();
        Service.PluginInterface.Inject(diagnostics);
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/enemies",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " + System.MapRenderer.EnemyDiagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/links",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " + System.MapRenderer.LinkDiagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/door",
            // /gratis door [map id]: the door lookup for an interior map; no id checks the Main Hall.
            Delegate = args => diagnostics.ChatGui.Print("[Gratis] "
                + (Rendering.MapDrawContext.Current.QuestLogSource is Controllers.QuestLogGame game
                    ? game.DoorDiagnostics(args.Length > 0 && uint.TryParse(args[0], out var map) ? map : 742)
                    : "door lookup unavailable")),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/memory",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " + System.MapRenderer.MemoryDiagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/window",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " + System.MapWindow.WindowPresetDiagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/camera",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " + System.MapRenderer.CameraDiagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/markers",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] "
                + Rendering.MapDrawContext.Current.MarkerRendering.Diagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/text",
            Delegate = _ => {
                Rendering.TextDrawing.SampleDiagnostics();
                diagnostics.ChatGui.Print("[Gratis] " + Rendering.MapDrawContext.Current.TextDiagnostics());
            },
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/attunement",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] "
                + System.MapWindow.PlacePalette.AttunementDiagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/subzone",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " + System.MapWindow.SubZoneDiagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/measure",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] "
                + Rendering.MapDrawContext.Current.Measure.Diagnostics()),
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
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] "
                + Rendering.MapDrawContext.Current.EdgePointers.Diagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/spotlight",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " + System.MapRenderer.Frame.Spotlight.Diagnostics()),
        });
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/motion",
            Delegate = _ => diagnostics.ChatGui.Print("[Gratis] " + System.MapRenderer.TakeMotionDiagnostics() + " "
                + Rendering.UiMotion.Diagnostics()),
        });
    }
}