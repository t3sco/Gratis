using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Gratis.Compat.CommandManager;
using Gratis.Compat.Window;
using Gratis.Classes;
using Gratis.Data;

namespace Gratis.Windows;

public class ConfigurationWindow : Window
{
    private readonly SettingsLayout layout = new();
    private readonly IconConfigurationTab icons = new();

    public ConfigurationWindow() : base("Gratis settings", new Vector2(780, 620))
    {
        System.CommandManager.RegisterCommand(new CommandHandler {
            Delegate = _ => System.ConfigWindow.Toggle(), ActivationPath = "/",
        });
    }

    public void OpenSection(string section)
    {
        var index = global::System.Array.IndexOf(SettingsLayout.Sections, section);
        if (index < 0) return;
        layout.SelectedSection = index; layout.Search = "";
        UnCollapseOrShow(); ImGui.SetWindowFocus(WindowName);
    }

    private SettingsTheme? theme;

    public override void PreDraw() => theme = SettingsTheme.Push(System.SystemConfig);
    public override void PostDraw() { theme?.Pop(); theme = null; }

    protected override void DrawContents()
    {
        if (!layout.Draw(System.SystemConfig, DrawExtensions, ResetExtensions)) return;
        if (System.MapWindow.SizeConstraints is { } constraints) {
            System.SystemConfig.WindowSize = Vector2.Max(System.SystemConfig.WindowSize, constraints.MinimumSize);
        }
        System.MapWindow.RefreshTitle();
        SystemConfig.Save();
    }

    private bool DrawExtensions(string section, string search)
    {
        if (section == "Marker options") {
            ImGui.PushItemWidth(180 * ImGui.GetFontSize() / 18);
            try { return MapFunctionsTab.DrawOwnedMarkers(); }
            finally { ImGui.PopItemWidth(); }
        }
        if (section == "Markers") {
            if (SettingsLayout.IconOverridesMatch(search)) icons.Draw();
            return false;
        }
        if (section != "Modules / tools") return false;
        var limit = System.FlagConfig.HistoryLimit;
        if (SettingsLayout.DrawTools(search, ref limit, OpenTool)) {
            System.FlagConfig.HistoryLimit = limit;
            System.FlagConfig.Save();
        }
        return false;
    }

    private static void ResetExtensions(string section)
    {
        if (section != "Modules / tools") return;
        System.FlagConfig.HistoryLimit = new FlagConfig().HistoryLimit;
        System.FlagConfig.Save();
    }

    private static void OpenTool(string tool)
    {
        Window? existing = tool switch {
            "FATE list" => System.WindowManager.GetWindow<FateListWindow>(),
            "Quest list" => System.WindowManager.GetWindow<QuestListWindow>(),
            "Flag history" => System.WindowManager.GetWindow<FlagHistoryWindow>(),
            _ => null,
        };
        if (existing is not null) { existing.UnCollapseOrShow(); return; }
        Window? window = tool switch {
            "FATE list" => new FateListWindow(),
            "Quest list" => new QuestListWindow(),
            "Flag history" => new FlagHistoryWindow(),
            _ => null,
        };
        if (window is not null) System.WindowManager.AddWindow(window, WindowFlags.OpenImmediately | WindowFlags.RequireLoggedIn);
    }
}

public partial class MapFunctionsTab
{
    public static bool DrawOwnedMarkers() => DrawMarkersSection();
}

public sealed class IconConfigurationTab
{
    private IconSetting? currentSetting;

    public void Draw()
    {
        if (!ImGui.CollapsingHeader("Per-icon overrides")) return;
        ImGui.TextDisabled("Select an observed game icon to override it.");
        if (Rendering.UiMotion.Child("icon-list", new Vector2(62, 270), System.SystemConfig.IsGratis ? ImGuiWindowFlags.NoScrollWithMouse : ImGuiWindowFlags.None)) {
            if (System.SystemConfig.IsGratis) Rendering.UiMotion.Scroll();
            foreach (var (iconId, settings) in System.IconConfig.IconSettingMap.OrderBy(pair => pair.Key)) {
                if (iconId == 0 || DrawHelpers.IsDisallowedIcon(iconId)) continue;
                var start = ImGui.GetCursorScreenPos();
                if (ImGui.Selectable($"##icon-{iconId}", currentSetting == settings, ImGuiSelectableFlags.None, new Vector2(36)))
                    currentSetting = settings;
                var texture = Service.TextureProvider.GetFromGameIcon(iconId).GetWrapOrEmpty();
                ImGui.GetWindowDrawList().AddImage(texture.Handle, start, start + new Vector2(32));
                if (ImGui.IsItemHovered()) Rendering.UiMotion.TooltipText($"Marker #{iconId}");
            }
        }
        ImGui.EndChild();
        ImGui.SameLine();
        if (Rendering.UiMotion.Child("icon-options", new Vector2(0, 270), System.SystemConfig.IsGratis ? ImGuiWindowFlags.NoScrollWithMouse : ImGuiWindowFlags.None) && currentSetting is { } setting) {
            if (System.SystemConfig.IsGratis) Rendering.UiMotion.Scroll();
            ImGui.TextUnformatted($"Marker #{setting.IconId}");
            var changed = ImGui.Checkbox("Hide icon", ref setting.Hide);
            changed |= ImGui.Checkbox("Allow tooltip", ref setting.AllowTooltip);
            changed |= ImGui.Checkbox("Allow click interaction", ref setting.AllowClick);
            ImGui.SetNextItemWidth(180);
            changed |= ImGui.ColorEdit4("Colour", ref setting.Color, ImGuiColorEditFlags.NoInputs);
            ImGui.SetNextItemWidth(180);
            changed |= ImGui.SliderFloat("Scale", ref setting.Scale, .05f, 20, "%.2fx");
            if (ImGui.Button("Reset this icon")) { setting.Reset(); changed = true; }
            if (changed) System.IconConfig.Save();
        }
        ImGui.EndChild();
    }
}
