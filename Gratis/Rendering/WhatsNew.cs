using System;
using System.Globalization;

namespace Gratis.Rendering;

public enum WhatsNewVerdict {
    Show, Classic, Disabled, SetupPending, UpToDate, LoggedOut, Loading, Duty, Cutscene, Combat, PvP, QuickSetup
}

public readonly record struct WhatsNewGates(bool LoggedIn, bool BetweenAreas, bool InDuty, bool InCutscene,
    bool InCombat, bool PvP, bool QuickSetupOpen);

public static class WhatsNew {
    public static WhatsNewVerdict Evaluate(MapDrawingConfig config, in WhatsNewGates gates) {
        if (!config.IsGratis)
            return WhatsNewVerdict.Classic;
        if (!config.ShowWhatsNew)
            return WhatsNewVerdict.Disabled;
        if (!config.QuickSetupDone)
            return WhatsNewVerdict.SetupPending;
        if (!IsOlder(config.LastSeenChangelogVersion, Changelog.Latest))
            return WhatsNewVerdict.UpToDate;
        if (!gates.LoggedIn)
            return WhatsNewVerdict.LoggedOut;
        if (gates.BetweenAreas)
            return WhatsNewVerdict.Loading;
        if (gates.InDuty)
            return WhatsNewVerdict.Duty;
        if (gates.InCutscene)
            return WhatsNewVerdict.Cutscene;
        if (gates.InCombat)
            return WhatsNewVerdict.Combat;
        if (gates.PvP)
            return WhatsNewVerdict.PvP;
        return gates.QuickSetupOpen ? WhatsNewVerdict.QuickSetup : WhatsNewVerdict.Show;
    }

    public static bool ShouldShow(MapDrawingConfig config, in WhatsNewGates gates) =>
        Evaluate(config, gates) == WhatsNewVerdict.Show;

    public static void Dismiss(MapDrawingConfig config) => config.LastSeenChangelogVersion = Changelog.Latest;

    // Empty or malformed counts as older: an existing user with no record sees the window once.
    public static bool IsOlder(string seen, string latest) {
        if (string.IsNullOrEmpty(seen))
            return true;
        var a = seen.AsSpan();
        var b = latest.AsSpan();
        for (var part = 0; part < 4; part++) {
            if (!Segment(ref a, out var x))
                return true;
            if (!Segment(ref b, out var y))
                return false;
            if (x != y)
                return x < y;
        }
        return false;
    }

    private static bool Segment(ref ReadOnlySpan<char> text, out int value) {
        value = 0;
        if (text.IsEmpty)
            return true;
        var dot = text.IndexOf('.');
        var part = dot < 0 ? text : text[..dot];
        if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out value))
            return false;
        text = dot < 0 ? default : text[(dot + 1)..];
        return true;
    }
}
