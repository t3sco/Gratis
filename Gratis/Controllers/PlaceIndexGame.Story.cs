using Gratis.Rendering;
using Gratis.Compat.CommandManager;

namespace Gratis.Controllers;

public sealed partial class PlaceIndexGame {
    private void RegisterStoryAction() => System.CommandManager.RegisterCommand(new CommandHandler {
        ActivationPath = "/msq", Delegate = _ => ShowMainStory(),
    });
    public void ShowMainStory() {
        refreshed = double.NegativeInfinity;
        Refresh();
        if (System.SystemConfig.IsGratis) {
            System.ZoneSwitchController?.NotifyUserAction();
            System.MapWindow.ProcessingCommand = true;
        }
        MapDrawContext.Current.Story.Show(System.MapRenderer.Frame, Entries, Context);
        Chat.Print("[Gratis] " + MapDrawContext.Current.Story.Diagnostics());
        if (System.SystemConfig.IsGratis)
            System.MapWindow.IsOpen = true;
    }
}