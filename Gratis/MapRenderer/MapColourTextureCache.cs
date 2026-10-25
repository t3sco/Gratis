using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private readonly HashSet<Task<Prepared>> retired = [];
    private MapColourParameters parameters = MapColourParameters.Identity;
    private long sequence;
    private bool disposed, failureLogged;
    private sealed record Prepared(MapColourPixels Pixels, MapColourPixels? Original = null, float Progress = 1);

    private sealed class Entry {
        public Func<MapColourPixels>? Load;
        public required MapColourParameters Parameters;
        public readonly CancellationTokenSource Cancellation = new();
        public Task<Prepared>? Work;
        public MapColourPixels? Original, Target;
        public IDalamudTextureWrap? Texture;
        public long Used, TransitionStart, Bytes;
        public float Progress;
        public bool Failed;
    }

    public MapColourTextureCache() => Service.Framework.Update += Update;

    public IDalamudTextureWrap
        ? Get(string source, ImTextureID textureId, MapColourParameters requested, Func<MapColourPixels> load) {
        lock (gate) {
            if (disposed)
                return null;
            if (parameters != requested) {
                Clear();
                parameters = requested;
            }
            if (requested.IsIdentity)
                return null;
            var key = new Key(source, textureId, requested);
            if (!entries.TryGetValue(key, out var entry)) {
                entry = new Entry { Load = load, Parameters = requested };
                entries.Add(key, entry);
            }
            entry.Used = ++sequence;
            if (entry.Texture is null)
                return null;
            return entry.Texture;
        }
    }

    private void Update(IFramework framework) {
        lock (gate) {
            if (disposed)
                return;
            foreach (var task in retired.Where(t => t.IsCompleted).ToArray()) {
                _ = task.Exception;
                retired.Remove(task);
            }
            foreach (var entry in entries.Values) {
                if (entry.Work is not { IsCompleted: true } work)
                    continue;
                entry.Work = null;
                try {
                    var prepared = work.GetAwaiter().GetResult();
                    var pixels = prepared.Pixels;
                    if (prepared.Original is not null) {
                        entry.Original = prepared.Original;
                        entry.Target = pixels;
                        entry.TransitionStart = Stopwatch.GetTimestamp();
                        continue;
                    }
                    entry.Bytes = checked((long)pixels.Width * pixels.Height * 4);
                    if (entry.Bytes > ByteLimit)
                        throw new InvalidOperationException("Map exceeds the colour cache byte limit.");
                    var texture = Service.TextureProvider.CreateFromRaw(RawImageSpecification.Rgba32(pixels.Width,
                        pixels.Height),
                        pixels.Rgba);
                    entry.Texture?.Dispose();
                    entry.Texture = texture;
                    entry.Progress = prepared.Progress;
                    if (entry.Progress >= 1) {
                        entry.Original = null;
                        entry.Target = null;
                    }
                }
                catch (OperationCanceledException) { entry.Failed = true; }
                catch (Exception exception) {
                    entry.Failed = true;
                    entry.Texture?.Dispose();
                    entry.Texture = null;
                    entry.Original = null;
                    entry.Target = null;
                    if (!failureLogged) {
                        Service.Log.Warning(exception, "[Map Colour] Recolour failed; using original artwork.");
                        failureLogged = true;
                    }
                }
            }
            while (entries.Count > Capacity || entries.Values.Sum(e => e.Texture is null ? 0 : e.Bytes) > ByteLimit)
                Remove(entries.MinBy(pair => pair.Value.Used).Key);

            var running = retired.Count + entries.Values.Count(e => e.Work is not null);
            foreach (var entry in entries.Values.OrderByDescending(e => e.Used)) {
                if (running >= 2)
                    break;
                if (entry.Work is not null || entry.Progress >= 1 || entry.Failed)
                    continue;
                var token = entry.Cancellation.Token;
                if (entry.Original is { } original && entry.Target is { } target) {
                    var progress = Math.Clamp((float)Stopwatch.GetElapsedTime(entry.TransitionStart).TotalMilliseconds
                        / 150 + 1f / 6,
                        0, 1);
                    if (progress <= entry.Progress)
                        continue;
                    entry.Work = Task.Run(() => new Prepared(target with {
                        Rgba = MapColour.Crossfade(original.Rgba, target.Rgba, progress, token),
                    }, Progress: progress), token);
                    running++;
                    continue;
                }
                var load = entry.Load!;
                entry.Load = null;
                entry.Work = Task.Run(() => {
                    token.ThrowIfCancellationRequested();
                    var pixels = load();
                    token.ThrowIfCancellationRequested();
                    var result = pixels with {
                        Rgba = MapColour.Apply(pixels.Rgba, entry.Parameters,
                        cancellationToken: token)
                    };
                    return new Prepared(result, pixels);
                }, token);
                running++;
            }
        }
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