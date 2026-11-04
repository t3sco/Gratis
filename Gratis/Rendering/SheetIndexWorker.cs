using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gratis.Rendering;

// Owned by the plugin; callers start/read/dispose on the framework thread. No native work in loaders.
public sealed class SheetIndexWorker<T> : IDisposable where T : class {
    private readonly CancellationTokenSource cancellation = new();
    private Task? task;
    private T? value;
    private string? error;
    private bool disposed;
    public T? Value => Volatile.Read(ref value);
    public string? Error => Volatile.Read(ref error);
    public void Ensure(Func<CancellationToken, T> load) {
        if (disposed || task != null) return;
        task = Start(load);
    }
    private Task Start(Func<CancellationToken, T> load) => Task.Run(() => {
            try {
                var result = load(cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                Volatile.Write(ref value, result);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception ex) { Volatile.Write(ref error, ex.Message); }
        });
    public void Dispose() {
        if (disposed) return;
        disposed = true;
        cancellation.Cancel();
        task?.GetAwaiter().GetResult(); // Join before plugin services are released.
        value = null;
        cancellation.Dispose();
    }
}
