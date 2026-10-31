using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Compat.Classes;
using Gratis.Compat.CommandManager;
using Gratis.Compat.Window;
using Gratis.Controllers;
using Gratis.Data;
using Gratis.Windows;

namespace Gratis;

public sealed class GratisPlugin : IDalamudPlugin {
    private readonly Gratis.Classes.ScreenshotCapture screenshotCapture;
    private readonly Gratis.Classes.GlassDiagnosticCommand glassDiagnosticCommand;
    private readonly Gratis.Rendering.LensCommand lensCommand;
    private readonly Gratis.Rendering.ReportLogBuffer reportLogs = new();
    private readonly Gratis.Classes.ReportPluginLog reportLogger;
    private readonly Gratis.Classes.ReportCommand reportCommand;
    private readonly Gratis.Classes.PvpDiagnosticCommand pvpDiagnosticCommand;
    private readonly Gratis.Classes.UpdateWatcher updateWatcher;
    private readonly AddonJournalController journalController;

    public GratisPlugin(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<Service>();
        reportLogger = new Gratis.Classes.ReportPluginLog(Service.Log, reportLogs);
        Service.Log = reportLogger;

        System.SystemConfig = SystemConfig.Load();
        System.FlagConfig = FlagConfig.Load();

        System.Teleporter = new Teleporter(Service.PluginInterface);

        System.CommandManager = new CommandManager(Service.PluginInterface, "gratis");

        System.MapRenderer = new MapRenderer.MapRenderer();

        System.ConfigWindow = new ConfigurationWindow();
        System.MapWindow = new MapWindow();
        Rendering.MapDrawContext.Current.Assistance = TravelIntegration.Assistance;

        System.WindowManager = new WindowManager(Service.PluginInterface);
        System.WindowManager.AddWindow(System.ConfigWindow, WindowFlags.IsConfigWindow | WindowFlags.RequireLoggedIn);
        System.WindowManager.AddWindow(System.MapWindow, WindowFlags.RequireLoggedIn);
        System.WindowManager.AddWindow(new WhatsNewWindow());
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/whatsnew", Delegate = _ => System.WindowManager.GetWindow<WhatsNewWindow>()?.ShowNow(),
        });

        screenshotCapture = new Gratis.Classes.ScreenshotCapture();
        glassDiagnosticCommand = new Gratis.Classes.GlassDiagnosticCommand();
        lensCommand = new Gratis.Rendering.LensCommand();

        System.FlagController = new FlagController();
        System.AreaMapController = new AddonAreaMapController();
        System.IntegrationsController = new IntegrationsController();
        System.ZoneSwitchController = new ZoneSwitchController();
        reportCommand = new Gratis.Classes.ReportCommand(screenshotCapture, reportLogs);
        pvpDiagnosticCommand = new Gratis.Classes.PvpDiagnosticCommand();

        Rendering.FeedbackLink.Open = Dalamud.Utility.Util.OpenLink;

        updateWatcher = new Gratis.Classes.UpdateWatcher();
        System.JournalController = journalController = new AddonJournalController();
        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/journal", Delegate = _ => journalController.Toggle(),
        });

        Service.PluginInterface.UiBuilder.OpenMainUi += OpenMapWindow;

        System.CommandManager.RegisterCommand(new ToggleCommandHandler {
            BaseActivationPath = "/fatelist",
            EnableDelegate = _ => System.WindowManager.OpenOrCreateUnique<FateListWindow>(WindowFlags.OpenImmediately
                    | WindowFlags.RequireLoggedIn),
            DisableDelegate = _ => System.WindowManager.GetWindow<FateListWindow>()?.Close(),
            ToggleDelegate = _ => System.WindowManager.GetWindow<FateListWindow>()?.UnCollapseOrToggle(),
        });

        System.CommandManager.RegisterCommand(new ToggleCommandHandler {
            BaseActivationPath = "/questlist",
            EnableDelegate = _ => System.WindowManager.OpenOrCreateUnique<QuestListWindow>(WindowFlags.OpenImmediately
                    | WindowFlags.RequireLoggedIn),
            DisableDelegate = _ => System.WindowManager.GetWindow<QuestListWindow>()?.Close(),
            ToggleDelegate = _ => System.WindowManager.GetWindow<QuestListWindow>()?.UnCollapseOrToggle(),
        });

        System.CommandManager.RegisterCommand(new ToggleCommandHandler {
            BaseActivationPath = "/flaglist",
            EnableDelegate = _ => System.WindowManager.OpenOrCreateUnique<FlagHistoryWindow>(WindowFlags.OpenImmediately
                    | WindowFlags.RequireLoggedIn),
            DisableDelegate = _ => System.WindowManager.GetWindow<FlagHistoryWindow>()?.Close(),
            ToggleDelegate = _ => System.WindowManager.GetWindow<FlagHistoryWindow>()?.UnCollapseOrToggle(),
        });
    }

    private unsafe void OpenMapWindow() => AgentMap.Instance()->Show();

    private static void Release(string name, global::System.Action step) {
        try {
            step();
        }
        catch (global::System.Exception error) {
            Service.Log.Error(error, "[Gratis] {Step} failed while unloading.", name);
        }
    }

    public void Dispose() {
        Release("journal controller", () => {
            journalController.Dispose();
            System.JournalController = null;
        });
        Release("update watcher", updateWatcher.Dispose);
        Release("pvp command", pvpDiagnosticCommand.Dispose);
        Release("report command", reportCommand.Dispose);
        Release("screenshot capture", screenshotCapture.Dispose);
        Release("glass command", glassDiagnosticCommand.Dispose);
        Release("lens command", lensCommand.Dispose);
        Release("zone switch", () => {
            System.ZoneSwitchController?.Dispose();
            System.ZoneSwitchController = null;
        });
        Release("map window", () => {
            System.MapWindow.OnClose();
            System.MapWindow.PlacePalette.Dispose();
            System.MapWindow.QuestLog.Dispose();
        });
        Release("windows", System.WindowManager.Dispose);
        Release("integrations", System.IntegrationsController.Dispose);
        Release("area map", System.AreaMapController.Dispose);
        Release("flag", System.FlagController.Dispose);
        Release("renderer", System.MapRenderer.Dispose);
        Release("draw context", () => (Rendering.MapDrawContext.Current as global::System.IDisposable)?.Dispose());
        Release("commands", System.CommandManager.Dispose);

        Service.PluginInterface.UiBuilder.OpenMainUi -= OpenMapWindow;
        reportLogger.Dispose();
    }
}
