using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace KamiLib.Classes;

public abstract class Cache<T, TU> where T : notnull {
    private readonly ConcurrentDictionary<T, TU?> internalCache = [];

    public TU? GetValue(T key) {
        if (internalCache.TryGetValue(key, out var result)) {
            return result;
        }
        
        else {
            
            if (internalCache.TryAdd(key, default)) {
                
                Task.Run(() => internalCache[key] = LoadValue(key));
                
                return internalCache[key];
            }
            
            else {
                return default;
            }
        }
    }

    protected abstract TU? LoadValue(T key);
}