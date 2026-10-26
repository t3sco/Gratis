using Gratis.Controllers;

namespace Gratis.Rendering;

public static class QuestLogIcon {
    public static uint Accepted(uint available, byte range, uint? section) {
        if (available != 0 && range != 0) {
            var offset = range >= 4 ? 3u : range > 1 ? 1u : 0u;
            if (available <= uint.MaxValue - offset)
                return available + offset;
        }
        return QuestBuckets.FromSection(section) switch {
            QuestBucket.Scenario => 71003,
            QuestBucket.JobClass => 71143,
            _ => 71023,
        };
    }
}
