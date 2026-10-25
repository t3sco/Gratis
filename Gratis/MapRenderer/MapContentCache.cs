using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gratis.Rendering;

namespace Gratis.MapRenderer;

internal sealed class MapContentCache(Func<string, MapColourPixels> load) : IDisposable
{
    private readonly Dictionary<(uint Map, string Path), Task<MapContentBounds?>> entries = [];
    private bool disposed;
    public MapContentBounds? Get(uint map, string path)
    {
        if (disposed) return null;
        var key = (map, path);
        if (!entries.TryGetValue(key, out var work)) {
            if (entries.Values.Count(task => !task.IsCompleted) >= 2) return null;
            if (entries.Count >= 32) entries.Remove(entries.First(pair => pair.Value.IsCompleted).Key);
            work = Task.Run(() => {
                try { var pixels = load(path); return MapContent.Scan(pixels.Rgba, pixels.Width, pixels.Height); }
                catch (Exception error) { Service.Log.Warning(error, "[Gratis] Small-room content scan unavailable; using map/player extents."); return null; }
            });
            entries[key] = work;
        }
        return work.IsCompletedSuccessfully ? work.Result : null;
    }
    public void Dispose() { disposed = true; entries.Clear(); }
}
