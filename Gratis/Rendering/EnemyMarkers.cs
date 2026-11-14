using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

// A separate background pass: these never enter MarkerBatch or affect priority markers.
public sealed class EnemyMarkers {
    public const int ScanLimit = 1024, MarkerLimit = 128;
    public int Shown { get; private set; }
    public int Needed { get; private set; }
    public bool Enabled { get; private set; }
    public void Begin(bool enabled, bool pvp) {
        Enabled = enabled && !pvp;
        Shown = Needed = 0;
    }

    // Striking dummies are drawn by the object pass as neutral markers, so the enemy pass skips them when asked to.
    public static bool IsVisibleEnemy(bool targetable, bool combatant, bool hostile, uint hp, float distanceSquared,
        bool dummy = false) =>
        targetable && combatant && hostile && !dummy && hp > 0 && float.IsFinite(distanceSquared)
        && distanceSquared < 22500;

    public bool Draw(Vector2 center, float scale, Vector4 accent, bool needed) {
        if (!Enabled || Shown >= MarkerLimit || !float.IsFinite(center.X) || !float.IsFinite(center.Y))
            return false;
        var list = ImGui.GetWindowDrawList();
        if (needed) {
            // Gratis gold with a soft glow so quest, FATE and hunting targets stand out from the ordinary dots.
            var gold = GratisTheme.WarmAccent;
            list.AddCircleFilled(center, 7f * scale, ImGui.GetColorU32(gold with { W = .12f }), 20);
            list.AddCircleFilled(center, 5.5f * scale, ImGui.GetColorU32(gold with { W = .22f }), 20);
            list.AddCircleFilled(center, 3.5f * scale, ImGui.GetColorU32(gold), 14);
            list.AddCircle(center, 3.5f * scale, ImGui.GetColorU32(new Vector4(.1f, .08f, .04f, .7f)), 14, scale);
        } else {
            list.AddCircleFilled(center, 3f * scale, ImGui.GetColorU32(new Vector4(1, 1, 1, .4f)), 12);
        }
        Shown++;
        if (needed) Needed++;
        return true;
    }

    public static string Details(byte level, string quest = "", string fate = "", bool huntingLog = false) {
        var text = $"Lv. {level}";
        if (quest.Length > 0) text += "\nQuest: " + quest;
        if (huntingLog) text += "\nHunting log";
        if (fate.Length > 0) text += "\nFATE: " + fate;
        return text;
    }

    public static string Tooltip(string name, byte level, string quest = "", string fate = "", bool huntingLog = false) =>
        name + "\n" + Details(level, quest, fate, huntingLog);

    public string Diagnostics() => $"enemies enabled={Enabled} shown={Shown} needed={Needed} cap={MarkerLimit}";
}
