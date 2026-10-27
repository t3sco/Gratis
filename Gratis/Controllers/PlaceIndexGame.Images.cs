using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;
using Gratis.Rendering;

namespace Gratis.Controllers;

internal sealed class PaletteGameImages : IDisposable {
    internal const int Capacity = 24;
    private sealed class Slot {
        public long Used;
        public Task<IDalamudTextureWrap?> Loading = null!;
    }

    private readonly Dictionary<string, Slot> cache = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource cancellation = new();
    private readonly SemaphoreSlim loaders = new(2);
    private bool disposed;

    public PaletteImage Get(PlaceEntry place) => Get(place.ArtPath, place.ArtPath, 0);
    public PaletteImage GetCrest(PlaceEntry place) => place.ExpansionIcon == 0 ? default
        : Get("crest:" + place.ExpansionIcon, "", place.ExpansionIcon);

    private PaletteImage Get(string key, string artPath, uint iconId) {
        if (disposed || key.Length == 0)
            return default;
        var now = Environment.TickCount64;
        if (!cache.TryGetValue(key, out var slot)) {
            if (cache.Count >= Capacity) {
                // Do not release a texture referenced by this frame's draw data.
                var oldest = cache.Where(p => now - p.Value.Used > 2000).OrderBy(p => p.Value.Used).FirstOrDefault();
                if (oldest.Key == null)
                    return default;
                cache.Remove(oldest.Key);
                Release(oldest.Value.Loading);
            }
            slot = new Slot { Loading = Task.Run(() => Load(artPath, iconId, cancellation.Token)) };
            cache.Add(key, slot);
        }
        slot.Used = now;
        if (!slot.Loading.IsCompletedSuccessfully || slot.Loading.Result is not { } wrap)
            return default;
        return new PaletteImage(wrap.Handle, wrap.Width, wrap.Height);
    }

    private async Task<IDalamudTextureWrap?> Load(string artPath, uint iconId, CancellationToken token) {
        var acquired = false;
        try {
            await loaders.WaitAsync(token).ConfigureAwait(false);
            acquired = true;
            if (iconId != 0)
                return await Service.TextureProvider.GetFromGameIcon(iconId).RentAsync(token).ConfigureAwait(false);
            if (artPath.Length > 0) {
                try {
                    return await Service.TextureProvider.GetFromGame(artPath).RentAsync(token).ConfigureAwait(
                        false);
                }
                catch (Exception) when (!token.IsCancellationRequested) {
                    try {
                        return await Service.TextureProvider.GetFromGame(artPath.Replace("_hr1.tex", ".tex",
                            StringComparison.Ordinal)).RentAsync(token).ConfigureAwait(false);
                    }
                    catch (Exception) when (!token.IsCancellationRequested) { }
                }
            }
            return null;
        }
        catch (Exception) { return null; }
        finally { if (acquired) loaders.Release(); }
    }

    private static void Release(Task<IDalamudTextureWrap?> loading) =>
        _ = loading.ContinueWith(task => { if (task.IsCompletedSuccessfully) task.Result?.Dispose(); },
            TaskScheduler.Default);

    public void Dispose() {
        disposed = true;
        cancellation.Cancel();
        foreach (var slot in cache.Values)
            Release(slot.Loading);
        cache.Clear();
    }
}

public sealed partial class PlaceIndexGame {
    public PaletteImage Crest(PlaceEntry place) => images.GetCrest(place);
}