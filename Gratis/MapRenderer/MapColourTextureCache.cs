using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Gratis.Rendering;

namespace Gratis.MapRenderer;

internal sealed record MapColourPixels(int Width, int Height, byte[] Rgba);

internal sealed class MapColourTextureCache : IDisposable {
    private const int Capacity = 8;
    private const long ByteLimit = 256L * 1024 * 1024;
    private readonly object gate = new();
    private readonly record struct Key(string Source, ImTextureID TextureId, MapColourParameters Parameters);
    private readonly Dictionary<Key, Entry> entries = [];
    private readonly HashSet<Task<MapColourPixels>> retired = [];
    private readonly List<Task<MapColourPixels>> completedRetired = [];
    private readonly List<Entry> ordered = [];
    private MapColourParameters parameters = MapColourParameters.Identity;
    private long sequence;
    private bool disposed, failureLogged;
    private long entriesCreated, texturesBuilt;

    private sealed class Entry {
        public Func<MapColourPixels>? Load;
        public required MapColourParameters Parameters;
        public readonly CancellationTokenSource Cancellation = new();
        public Task<MapColourPixels>? Work;
        public IDalamudTextureWrap? Texture;
        public long Used, Bytes;
        public bool Failed;
    }

    public MapColourTextureCache() => Service.Framework.Update += Update;

    // `pending` means a recolour is still being built and nothing is held for this map: draw nothing for those
    // frames, because the original artwork would flash up in the wrong colours before the recolour replaces it.
    public IDalamudTextureWrap? Get(string source, ImTextureID textureId, MapColourParameters requested,
        Func<MapColourPixels> load, out bool pending) {
        pending = false;
        lock (gate) {
            if (disposed)
                return null;
            if (parameters != requested) {
                Clear();
                parameters = requested;
            }
            if (requested.IsIdentity)
                return null;
            // The game map texture can be released while the window is closed or between frames of a reopen, and
            // comes back under a new handle. Nothing is built for an empty handle, and the recoloured map keeps
            // showing until its replacement is ready, so the original artwork never flashes through.
            if (textureId == default)
                return Held(source, null);
            var key = new Key(source, textureId, requested);
            if (!entries.TryGetValue(key, out var entry)) {
                entry = new Entry { Load = load, Parameters = requested };
                entries.Add(key, entry);
                entriesCreated++;
            }
            entry.Used = ++sequence;
            var texture = entry.Texture ?? Held(source, entry);
            pending = texture is null && !entry.Failed;
            return texture;
        }
    }

    private IDalamudTextureWrap? Held(string source, Entry? except) {
        Entry? best = null;
        foreach (var pair in entries)
            if (pair.Key.Source == source && pair.Value != except && pair.Value.Texture is not null
                && (best is null || pair.Value.Used > best.Used))
                best = pair.Value;
        if (best is null)
            return null;
        best.Used = Math.Max(best.Used, sequence);
        return best.Texture;
    }

    private void Update(IFramework framework) {
        lock (gate) {
            if (disposed)
                return;
            var active = entries.Count > Capacity;
            foreach (var entry in entries.Values)
                active |= entry.Work is not null || entry.Load is not null && !entry.Failed;
            if (!active && retired.Count == 0 && TextureBytes() <= ByteLimit)
                return;
            foreach (var task in retired)
                if (task.IsCompleted)
                    completedRetired.Add(task);
            foreach (var task in completedRetired) {
                _ = task.Exception;
                retired.Remove(task);
            }
            completedRetired.Clear();
            foreach (var entry in entries.Values) {
                if (entry.Work is not { IsCompleted: true } work)
                    continue;
                entry.Work = null;
                try {
                    var pixels = work.GetAwaiter().GetResult();
                    entry.Bytes = checked((long)pixels.Width * pixels.Height * 4);
                    if (entry.Bytes > ByteLimit)
                        throw new InvalidOperationException("Map exceeds the colour cache byte limit.");
                    var texture = Service.TextureProvider.CreateFromRaw(RawImageSpecification.Rgba32(pixels.Width,
                        pixels.Height),
                        pixels.Rgba);
                    entry.Texture?.Dispose();
                    entry.Texture = texture;
                    texturesBuilt++;
                }
                catch (OperationCanceledException) { entry.Failed = true; }
                catch (Exception exception) {
                    entry.Failed = true;
                    entry.Texture?.Dispose();
                    entry.Texture = null;
                    if (!failureLogged) {
                        Service.Log.Warning(exception, "[Map Colour] Recolour failed; using original artwork.");
                        failureLogged = true;
                    }
                }
            }
            while (entries.Count > Capacity || TextureBytes() > ByteLimit) {
                var oldest = default(Key);
                var used = long.MaxValue;
                var found = false;
                foreach (var pair in entries)
                    if (!found || pair.Value.Used < used) {
                        oldest = pair.Key;
                        used = pair.Value.Used;
                        found = true;
                    }
                Remove(oldest);
            }

            var running = retired.Count;
            foreach (var entry in entries.Values) {
                if (entry.Work is not null) running++;
                var index = ordered.Count;
                while (index > 0 && ordered[index - 1].Used < entry.Used) index--;
                ordered.Insert(index, entry);
            }
            foreach (var entry in ordered) {
                if (running >= 2)
                    break;
                if (entry.Work is not null || entry.Load is null || entry.Failed)
                    continue;
                var token = entry.Cancellation.Token;
                var load = entry.Load!;
                entry.Load = null;
                entry.Work = Task.Run(() => {
                    token.ThrowIfCancellationRequested();
                    var pixels = load();
                    token.ThrowIfCancellationRequested();
                    return pixels with {
                        Rgba = MapColour.Apply(pixels.Rgba, entry.Parameters, cancellationToken: token)
                    };
                }, token);
                running++;
            }
            ordered.Clear();
        }
    }

    // Counts that climb steadily while the map sits still mean the cache is rebuilding recolours it should be reusing.
    public string Diagnostics() {
        lock (gate)
            return $"colourCache entries={entries.Count} texMB={TextureBytes() / 1048576} retired={retired.Count} "
                + $"created={entriesCreated} built={texturesBuilt}";
    }

    private long TextureBytes() {
        long bytes = 0;
        foreach (var entry in entries.Values)
            if (entry.Texture is not null) bytes += entry.Bytes;
        return bytes;
    }

    private void Remove(Key key) {
        var entry = entries[key];
        entry.Cancellation.Cancel();
        entry.Cancellation.Dispose();
        if (entry.Work is { } work)
            retired.Add(work);
        entry.Texture?.Dispose();
        entries.Remove(key);
    }

    private void Clear() {
        foreach (var key in entries.Keys.ToArray())
            Remove(key);
    }

    public void Dispose() {
        Service.Framework.Update -= Update;
        lock (gate) {
            disposed = true;
            Clear();
            foreach (var work in retired)
                _ = work.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default);
            retired.Clear();
        }
    }
}
