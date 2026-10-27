using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface;
using Gratis.Rendering;

namespace Gratis.Classes;

internal sealed class UpdateWatcher : IDisposable {
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    // Never disposed: a check still in flight when Gratis unloads must be able to see that it was cancelled.
    private readonly CancellationTokenSource stop = new();

    public UpdateWatcher() {
        UpdateNotice.OpenInstaller = () =>
            Service.PluginInterface.OpenPluginInstallerTo(PluginInstallerOpenKind.UpdateablePlugins);
        _ = Task.Run(Watch);
    }

    private async Task Watch() {
        var current = typeof(UpdateWatcher).Assembly.GetName().Version;
        try {
            while (!stop.IsCancellationRequested) {
                try {
                    var update = await Service.PluginInterface.CheckForUpdateAsync().ConfigureAwait(false);
                    if (stop.IsCancellationRequested)
                        return;
                    UpdateNotice.Available = update is { } found && current is not null && found.Version > current
                        ? found.Version.ToString() : "";
                }
                catch (Exception error) when (error is not OperationCanceledException) {
                    Service.Log.Debug(error, "[Gratis] Update check failed.");
                }
                await Task.Delay(Interval, stop.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) {
        }
    }

    public void Dispose() {
        stop.Cancel();
        UpdateNotice.OpenInstaller = null;
        UpdateNotice.Available = "";
    }
}
