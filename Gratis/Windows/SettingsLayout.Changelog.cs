using System;
using System.Numerics;
using Gratis.Rendering;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Windows;

public sealed partial class SettingsLayout {
    private static void DrawChangelog() {
        var s = SettingsTheme.Scale;
        using (SettingsTheme.Font(14, ChromeFontRole.Title)) {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("What's new in Gratis");
        }
        SettingsTheme.Helper("The good stuff, newest first.");
        var first = true;
        foreach (var release in Changelog.Releases) {
            ImGui.Spacing();
            ImGui.PushID(release.Version);
            using (SettingsTheme.Font(14, ChromeFontRole.Title)) {
                ImGui.TextColored(GratisTheme.WarmAccent, release.Headline);
            }
            using (SettingsTheme.Font(12)) {
                ImGui.TextColored(GratisTheme.Secondary, first ? $"{release.Version}  /  {release.Date}  /  latest"
                    : $"{release.Version}  /  {release.Date}");
            }
            first = false;
            var indent = 16 * s;
            var list = ImGui.GetWindowDrawList();
            foreach (var group in release.Groups) {
                var tint = group.Kind switch {
                    ChangeKind.New => GratisTheme.Crystal,
                    ChangeKind.Improved => GratisTheme.WarmAccent,
                    _ => GratisTheme.Secondary,
                };
                ImGui.Dummy(new(0, 2 * s));
                using (SettingsTheme.Font(12))
                    ImGui.TextColored(tint, Changelog.Label(group.Kind).ToUpperInvariant());
                foreach (var line in group.Items) {
                    var pos = ImGui.GetCursorScreenPos();
                    list.AddCircleFilled(pos + new Vector2(5 * s, ImGui.GetTextLineHeight() / 2), 2 * s,
                        ImGui.GetColorU32(tint));
                    ImGui.Indent(indent);
                    ImGui.PushTextWrapPos(0);
                    ImGui.TextUnformatted(line);
                    ImGui.PopTextWrapPos();
                    ImGui.Unindent(indent);
                }
            }
            ImGui.PopID();
            ImGui.Spacing();
            ImGui.Separator();
        }
    }
}
