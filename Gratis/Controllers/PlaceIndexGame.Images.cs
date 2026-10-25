using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;
using Gratis.Rendering;

namespace Gratis.Controllers;

internal sealed class PaletteGameImages : IDisposable
{
    internal const int Capacity = 24;
    private sealed class Slot
    {
        public long Used;
        public Task<IDalamudTextureWrap?> Loading = null!;
    }
    private readonly Dictionary<string, Slot> cache = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource cancellation = new();
    private readonly SemaphoreSlim loaders = new(2);
    private bool disposed;

    public PaletteImage Get(PlaceEntry place)
    {
        if (disposed) return default;
        var key = place.ArtPath.Length > 0 ? place.ArtPath : place.ExpansionIcon != 0 ? "icon:" + place.ExpansionIcon : "";
        if (key.Length == 0) return default;
        var now = Environment.TickCount64;
        if (!cache.TryGetValue(key, out var slot)) {
            if (cache.Count >= Capacity) {
                var oldest = cache.Where(p => now - p.Value.Used > 2000).OrderBy(p => p.Value.Used).FirstOrDefault();
                if (oldest.Key == null) return default;
                cache.Remove(oldest.Key); Release(oldest.Value.Loading);
            }
            slot = new Slot { Loading = Task.Run(() => Load(place, cancellation.Token)) };
            cache.Add(key, slot);
        }
        slot.Used = now;
        if (!slot.Loading.IsCompletedSuccessfully || slot.Loading.Result is not { } wrap) return default;
        return new PaletteImage(wrap.Handle, wrap.Width, wrap.Height);
    }

    private async Task<IDalamudTextureWrap?> Load(PlaceEntry place, CancellationToken token)
    {
        var acquired = false;
        try {
            await loaders.WaitAsync(token).ConfigureAwait(false); acquired = true;
            if (place.ArtPath.Length > 0) {
                try { return await Service.TextureProvider.GetFromGame(place.ArtPath).RentAsync(token).ConfigureAwait(false); }
                catch (Exception) when (!token.IsCancellationRequested) {
                    try { return await Service.TextureProvider.GetFromGame(place.ArtPath.Replace("_hr1.tex", ".tex", StringComparison.Ordinal)).RentAsync(token).ConfigureAwait(false); }
                    catch (Exception) when (!token.IsCancellationRequested) { }
                }
            }
            return place.ExpansionIcon == 0 ? null : await Service.TextureProvider.GetFromGameIcon(place.ExpansionIcon).RentAsync(token).ConfigureAwait(false);
        }
        catch (Exception) { return null; }
        finally { if (acquired) loaders.Release(); }
    }

    private static void Release(Task<IDalamudTextureWrap?> loading) => _ = loading.ContinueWith(
        task => { if (task.IsCompletedSuccessfully) task.Result?.Dispose(); }, TaskScheduler.Default);

    public void Dispose()
    {
        disposed = true; cancellation.Cancel();
        foreach (var slot in cache.Values) Release(slot.Loading);
        cache.Clear();
    }
}
