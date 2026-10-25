/*
    Copyright (C) 2022  MidoriKami

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.Collections.Concurrent;
using System;
using System.Threading.Tasks;

namespace Gratis.Compat.Classes;

public abstract class Cache<T, TU> where T : notnull {
    private readonly ConcurrentDictionary<T, TU?> internalCache = [];

    public TU? GetValue(T key) {
        if (internalCache.TryGetValue(key, out var result)) {
            return result;
        }

        if (!internalCache.TryAdd(key, default)) {
            return default;
        }

        Task.Run(() => Load(key));

        return internalCache[key];
    }

    private void Load(T key) {
        try {
            internalCache[key] = LoadValue(key);
        }
        catch (Exception e) {
            Service.Log.Error(e, $"Failed to load cache entry for {key}");
        }
    }

    protected abstract TU? LoadValue(T key);
}