using Dalamud.Interface.GameFonts;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Classes;
using KamiLib.CommandManager;
using KamiLib.Window;
using Gratis.Controllers;
using Gratis.Data;
using Gratis.Windows;

namespace Gratis;

public sealed class GratisPlugin : IDalamudPlugin
{
    private readonly Gratis.Classes.ScreenshotCapture screenshotCapture;
    private readonly Gratis.Classes.GlassDiagnosticCommand glassDiagnosticCommand;
    private readonly Gratis.Rendering.LensCommand lensCommand;

    public GratisPlugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();

        System.LargeAxisFontHandle = Service.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle
        {
            SizePt = 72.0f,
            FamilyAndSize = GameFontFamilyAndSize.Axis36,
            Italic = true,
            BaseSkewStrength = 16f,
        });

        System.SystemConfig = SystemConfig.Load();
        System.IconConfig = IconConfig.Load();
        System.FlagConfig = FlagConfig.Load();

        System.Teleporter = new Teleporter(Service.PluginInterface);

        System.CommandManager = new CommandManager(Service.PluginInterface, "gratis");

        System.MapRenderer = new MapRenderer.MapRenderer();

        System.ConfigWindow = new ConfigurationWindow();
        System.MapWindow = new MapWindow();

        System.WindowManager = new WindowManager(Service.PluginInterface);
        System.WindowManager.AddWindow(System.ConfigWindow, WindowFlags.IsConfigWindow | WindowFlags.RequireLoggedIn);
        System.WindowManager.AddWindow(System.MapWindow, WindowFlags.RequireLoggedIn);

        screenshotCapture = new Gratis.Classes.ScreenshotCapture();
        glassDiagnosticCommand = new Gratis.Classes.GlassDiagnosticCommand();
        lensCommand = new Gratis.Rendering.LensCommand();

        System.FlagController = new FlagController();
        System.AreaMapController = new AddonAreaMapController();
        System.IntegrationsController = new IntegrationsController();
        System.ZoneSwitchController = new ZoneSwitchController();

        Service.PluginInterface.UiBuilder.OpenMainUi += OpenMapWindow;

        System.CommandManager.RegisterCommand(new ToggleCommandHandler
        {
            BaseActivationPath = "/fatelist",
            EnableDelegate = _ => System.WindowManager.OpenOrCreateUnique<FateListWindow>(WindowFlags.OpenImmediately | WindowFlags.RequireLoggedIn),
            DisableDelegate = _ => System.WindowManager.GetWindow<FateListWindow>()?.Close(),
            ToggleDelegate = _ => System.WindowManager.GetWindow<FateListWindow>()?.UnCollapseOrToggle(),
        });

        System.CommandManager.RegisterCommand(new ToggleCommandHandler
        {
            BaseActivationPath = "/questlist",
            EnableDelegate = _ => System.WindowManager.OpenOrCreateUnique<QuestListWindow>(WindowFlags.OpenImmediately | WindowFlags.RequireLoggedIn),
            DisableDelegate = _ => System.WindowManager.GetWindow<QuestListWindow>()?.Close(),
            ToggleDelegate = _ => System.WindowManager.GetWindow<QuestListWindow>()?.UnCollapseOrToggle(),
        });

        System.CommandManager.RegisterCommand(new ToggleCommandHandler
        {
            BaseActivationPath = "/flaglist",
            EnableDelegate = _ => System.WindowManager.OpenOrCreateUnique<FlagHistoryWindow>(WindowFlags.OpenImmediately | WindowFlags.RequireLoggedIn),
            DisableDelegate = _ => System.WindowManager.GetWindow<FlagHistoryWindow>()?.Close(),
            ToggleDelegate = _ => System.WindowManager.GetWindow<FlagHistoryWindow>()?.UnCollapseOrToggle(),
        });
    }

    private unsafe void OpenMapWindow() => AgentMap.Instance()->Show();

    public void Dispose()
    {
        screenshotCapture.Dispose();
        glassDiagnosticCommand.Dispose();
        lensCommand.Dispose();
        System.ZoneSwitchController?.Dispose();
        System.ZoneSwitchController = null;
        System.MapWindow.OnClose();
        System.WindowManager.Dispose();
        System.IntegrationsController.Dispose();
        System.AreaMapController.Dispose();
        System.FlagController.Dispose();
        System.MapRenderer.Dispose();
        (Rendering.MapDrawContext.Current as global::System.IDisposable)?.Dispose();

        Service.PluginInterface.UiBuilder.OpenMainUi -= OpenMapWindow;
    }
}
