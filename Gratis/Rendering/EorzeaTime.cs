namespace Gratis.Rendering;

public static class EorzeaTime {
    private sealed record Formatted(int Minute, string Text);
    private static Formatted formatted = new(-1, "");
    public static int MinuteOfDay(long unixSeconds) => (int)(((unixSeconds % 4200 + 4200) % 4200) * 12 / 35);
    public static string Format(long unixSeconds) {
        var minute = MinuteOfDay(unixSeconds);
        var cached = formatted;
        if (cached.Minute == minute)
            return cached.Text;
        var text = global::System.FormattableString.Invariant($"ET {minute / 60:00}:{minute % 60:00}");
        formatted = new(minute, text);
        return text;
    }
}
