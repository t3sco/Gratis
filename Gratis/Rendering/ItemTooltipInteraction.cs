using System.Collections.Generic;

namespace Gratis.Rendering;

public sealed class ItemTooltipHover {
    public const double Intent = .12;
    public const double Grace = .15;
    private uint candidate;
    private double entered, lastHover = double.NegativeInfinity, lastShown = double.NegativeInfinity;

    public bool Observe(uint id, double now) {
        var warm = Visible(now);
        if (candidate != id || now - lastHover > Grace) entered = now;
        candidate = id;
        lastHover = now;
        if (!warm && now - entered + .000001 < Intent) return false;
        lastShown = now;
        return true;
    }

    public bool Visible(double now) => now >= lastShown && now - lastShown <= Grace;

    public void EndFrame(bool hovered, double now) {
        if (!hovered && !Visible(now)) {
            candidate = 0;
            lastHover = double.NegativeInfinity;
        }
    }

    public void Reset() {
        candidate = 0;
        lastHover = lastShown = double.NegativeInfinity;
    }
}

public sealed class ItemTooltipCache {
    public const int Capacity = 64;
    public const int LoadsPerFrame = 4;
    private readonly Dictionary<uint, ItemTooltipRecord?> items = [];
    private readonly Queue<uint> pending = [];
    private IItemTooltipSource? source;
    private IReadOnlyList<QuestLogRewardItem>? rewards;

    public void Prepare(IReadOnlyList<QuestLogRewardItem>? current, IItemTooltipSource? provider) {
        if (!ReferenceEquals(source, provider) || !ReferenceEquals(rewards, current)) {
            source = provider;
            rewards = current;
            items.Clear();
            pending.Clear();
            if (current != null && provider != null)
                foreach (var item in current) {
                    if (items.ContainsKey(item.Id)) continue;
                    if (items.Count == Capacity) break;
                    items[item.Id] = null;
                    pending.Enqueue(item.Id);
                }
        }
        for (var i = 0; i < LoadsPerFrame && pending.TryDequeue(out var id); i++)
            items[id] = source?.Get(id);
    }

    public ItemTooltipRecord? Get(uint id) => items.GetValueOrDefault(id);
}
