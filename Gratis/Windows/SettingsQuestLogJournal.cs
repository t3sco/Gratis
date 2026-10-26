using System.Numerics;
using Gratis.Rendering;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Windows;

public static class SettingsQuestLogJournal {
#if GRATIS_PREVIEW
    public static Vector4 ToggleBounds;
#endif
    public static bool Draw(MapDrawingConfig config) {
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = 12 * SettingsTheme.Scale;
        ImGui.SetCursorScreenPos(start + new Vector2(pad));
        ImGui.BeginGroup();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width - pad * 2);
        ImGui.TextWrapped("Open the Gratis Quest Log from your Journal key, Duty menu, /journal or quest links.");
        var changed = SettingsTheme.Toggle("##journal-replacement", ref config.QuestLogReplaceJournal, GratisTheme.WarmAccent);
#if GRATIS_PREVIEW
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        ToggleBounds = new(min.X, min.Y, max.X, max.Y);
#endif
        ImGui.SameLine();
        ImGui.TextWrapped("Use the Gratis Quest Log instead of the game's Journal");
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();
        var bottom = ImGui.GetItemRectMax().Y + pad;
        ImGui.GetWindowDrawList().AddRect(start, new(start.X + width, bottom), ImGui.GetColorU32(GratisTheme.Border),
            GratisTheme.Radius.Card * SettingsTheme.Scale);
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new(width, bottom - start.Y));
        if (changed) MapDrawContext.Current.JournalReplacementChanged();
        return changed;
    }
}
