namespace Gratis.Rendering;

internal static class FlagHistoryRetention {
    // History is newest first; preserve that ordering while dropping every excess entry.
    public static void Trim<T>(global::System.Collections.Generic.LinkedList<T> history, int limit) {
        limit = global::System.Math.Max(0, limit);
        while (history.Count > limit)
            history.RemoveLast();
    }
}
