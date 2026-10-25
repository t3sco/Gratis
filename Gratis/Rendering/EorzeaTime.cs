namespace Gratis.Rendering;

public static class EorzeaTime {
    public static int MinuteOfDay(long unixSeconds) => (int)(((unixSeconds % 4200 + 4200) % 4200) * 12 / 35);
    public static string Format(long unixSeconds) {
        var minute = MinuteOfDay(unixSeconds);
        return global::System.FormattableString.Invariant($"ET {minute / 60:00}:{minute % 60:00}");
    }
}