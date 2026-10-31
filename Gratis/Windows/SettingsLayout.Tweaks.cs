using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Windows;

public sealed partial class SettingsLayout {
    public bool ModifiedTweaksOnly;
#if GRATIS_PREVIEW
    public readonly global::System.Collections.Generic.Dictionary<string, Vector2> TestTweakActions = [];
    private void RecordTweakAction(string id) =>
        TestTweakActions[id] = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax())
        / 2;
#endif

    private bool DrawTweaks(SystemConfig config) {
        var changed = false;
        var s = SettingsTheme.Scale;
#if GRATIS_PREVIEW
        TestTweakActions.Clear();
#endif
        ImGui.TextUnformatted($"{TweakCatalog.All.Count} small quality-of-life tweaks");
        var modified = ModifiedTweaksOnly;
        if (SettingsTheme.Toggle("##modified-tweaks", ref modified, "Modified only"))
            ModifiedTweaksOnly = modified;
#if GRATIS_PREVIEW
        RecordTweakAction("modified");
#endif
        ImGui.Spacing();
        var results = TweakCatalog.Find(config, Search, ModifiedTweaksOnly);
        ImGui.TextDisabled($"{results.Length} shown / {TweakCatalog.All.Count(t => t.IsModified(config))} modified");
        if (results.Length == 0)
            SettingsTheme.Helper("No tweaks match. Try another name or turn off Modified only.");
        foreach (var group in results.GroupBy(t => t.Group)) {
            ImGui.Spacing();
            ImGui.TextColored(GratisTheme.WarmAccent, group.Key);
            ImGui.Separator();
            foreach (var tweak in group) {
                ImGui.PushID(tweak.Id);
                if (ImGui.BeginTable("tweak", 3, ImGuiTableFlags.SizingStretchProp)) {
                    ImGui.TableSetupColumn("Tweak", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 36 * s);
                    ImGui.TableSetupColumn("Reset", ImGuiTableColumnFlags.WidthFixed, 36 * s);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    var pos = ImGui.GetCursorScreenPos();
                    var width = MathF.Max(1, ImGui.GetContentRegionAvail().X);
                    var list = ImGui.GetWindowDrawList();
                    GratisIcons.Draw(list, pos + new Vector2(8, 10) * s, tweak.Icon, GratisTheme.Secondary, 14 * s);
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 24 * s);
                    ImGui.TextUnformatted(TweakEllipsis(tweak.Name, width - 24 * s));
                    if (ImGui.IsItemHovered())
                        UiMotion.TooltipText(tweak.Name, scale: SettingsTheme.Scale);
                    using (SettingsTheme.Font(12)) {
                        ImGui.TextColored(GratisTheme.Secondary, TweakEllipsis(tweak.Description, width));
                        if (ImGui.IsItemHovered())
                            UiMotion.TooltipText(tweak.Description, scale: SettingsTheme.Scale);
                        if (tweak.IntegrationNote is { } note) {
                            ImGui.TextColored(GratisTheme.Secondary, TweakEllipsis(note, width));
                            if (ImGui.IsItemHovered())
                                UiMotion.TooltipText(note, scale: SettingsTheme.Scale);
                        }
                    }
                    if (tweak.ConfigureSection is not null) {
                        if (ImGui.SmallButton("Configure  >"))
                            ConfigureTweak(tweak, config);
#if GRATIS_PREVIEW
                        RecordTweakAction(tweak.Id + "/configure");
#endif
                    }
                    ImGui.TableNextColumn();
                    var value = tweak.Get(config);
#if GRATIS_PREVIEW
                    var control = ImGui.GetCursorScreenPos();
                    TestRows.Add((tweak.ConfigPath, new(pos.X, pos.Y, pos.X + width, pos.Y + 36 * s),
                        new(control.X, control.Y, control.X + 36 * s, control.Y + 36 * s)));
#endif
                    ImGui.BeginDisabled(tweak.Locked);
                    if (SettingsTheme.Toggle("##toggle", ref value)) {
                        tweak.Set(config, value);
                        changed = true;
                    }
                    ImGui.EndDisabled();
#if GRATIS_PREVIEW
                    RecordTweakAction(tweak.Id + "/toggle");
#endif
                    if (ImGui.IsItemHovered())
                        UiMotion.TooltipText($"{tweak.Name}. Default: {(tweak.Default ? "on" : "off")}.", scale: SettingsTheme.Scale);
                    ImGui.TableNextColumn();
                    ImGui.BeginDisabled(!tweak.IsModified(config));
                    if (ChromeControls.IconButton("##reset", GratisIcons.Repeat, "Reset " + tweak.Name,
                        new Vector2(36) * s, s)) {
                        tweak.Reset(config);
                        changed = true;
                    }
#if GRATIS_PREVIEW
                    RecordTweakAction(tweak.Id + "/reset");
#endif
                    ImGui.EndDisabled();
                    ImGui.EndTable();
                }
                ImGui.PopID();
            }
        }
        return changed;
    }

    /// <summary>
    /// Opens the page that holds a tweak's setting. Gratis picks the tab and sub-tab the row sits under, so no search
    /// is needed; Classic keeps its section page and a search for the group.
    /// </summary>
    public void ConfigureTweak(TweakDefinition tweak, SystemConfig? config = null) {
        if (tweak.ConfigureSection is null)
            return;
        SelectedSection = Array.IndexOf(Sections, tweak.ConfigureSection);
        var group = tweak.ConfigureGroup ?? "";
        if (config is { IsGratis: false }) {
            Search = group;
            return;
        }
        var entry = Array.Find(Entries, e => e.Path == tweak.ConfigPath);
        var home = entry is null ? Home(tweak.ConfigureSection, group) : Home(entry.Section, entry.Group);
        SelectTab(home.Tab, home.Sub);
        // A locked tweak's row is never drawn outside a search, so it keeps its group search.
        Search = tweak.Locked ? group : "";
    }

    private static string TweakEllipsis(string text, float width) {
        if (ImGui.CalcTextSize(text).X <= width)
            return text;
        const string ellipsis = "...";
        var boundaries = StringInfo.ParseCombiningCharacters(text);
        var length = boundaries.Length;
        while (length > 0) {
            var candidate = text[..boundaries[--length]] + ellipsis;
            if (ImGui.CalcTextSize(candidate).X <= width)
                return candidate;
        }
        return ImGui.CalcTextSize(ellipsis).X <= width ? ellipsis : "";
    }
}