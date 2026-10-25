using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Gratis.Compat.CommandManager;
using Gratis.Compat.Window;
using Gratis.Classes;
using Gratis.Data;

namespace Gratis.Windows;

public class ConfigurationWindow : Window {
    private readonly SettingsLayout layout = new();

    public ConfigurationWindow() : base("Gratis settings", new Vector2(780, 620)) {
        System.CommandManager.RegisterCommand(new CommandHandler {
            Delegate = _ => System.ConfigWindow.Toggle(), ActivationPath = "/",
        });
    }

    public void OpenSection(string section) {
        var index = global::System.Array.IndexOf(SettingsLayout.Sections, section);
        if (index < 0)
            return;
        layout.SelectedSection = index;
        layout.Search = "";
        UnCollapseOrShow();
        ImGui.SetWindowFocus(WindowName);
    }

    private SettingsTheme? theme;

    public override void PreDraw() => theme = SettingsTheme.Push(System.SystemConfig);
    public override void PostDraw() {
        theme?.Pop();
        theme = null;
    }

    protected override void DrawContents() {
        if (!layout.Draw(System.SystemConfig, DrawExtensions, ResetExtensions))
            return;
        if (System.MapWindow.SizeConstraints is { } constraints) {
            System.SystemConfig.WindowSize = Vector2.Max(System.SystemConfig.WindowSize, constraints.MinimumSize);
        }
        System.MapWindow.RefreshTitle();
        SystemConfig.Save();
    }

    private bool DrawExtensions(string section, string search) {
        if (section == "Marker options") {
            ImGui.PushItemWidth(180 * ImGui.GetFontSize() / 18);
            try {
                return MapFunctionsTab.DrawOwnedMarkers();
            }
            finally { ImGui.PopItemWidth(); }
        }
        if (section != "Modules / tools")
            return false;
        var limit = System.FlagConfig.HistoryLimit;
        if (SettingsLayout.DrawTools(search, ref limit, OpenTool)) {
            System.FlagConfig.HistoryLimit = limit;
            System.FlagConfig.Save();
        }
        return false;
    }

    private static void ResetExtensions(string section) {
        if (section != "Modules / tools")
            return;
        System.FlagConfig.HistoryLimit = new FlagConfig().HistoryLimit;
        System.FlagConfig.Save();
    }

    private static void OpenTool(string tool) {
        Window? existing = tool switch {
            "FATE list" => System.WindowManager.GetWindow<FateListWindow>(),
            "Quest list" => System.WindowManager.GetWindow<QuestListWindow>(),
            "Flag history" => System.WindowManager.GetWindow<FlagHistoryWindow>(),
            _ => null,
        };
        if (existing is not null) {
            existing.UnCollapseOrShow();
            return;
        }
        Window? window = tool switch {
            "FATE list" => new FateListWindow(),
            "Quest list" => new QuestListWindow(),
            "Flag history" => new FlagHistoryWindow(),
            _ => null,
        };
        if (window is not null)
            System.WindowManager.AddWindow(window, WindowFlags.OpenImmediately | WindowFlags.RequireLoggedIn);
    }
}

public partial class MapFunctionsTab {
    public static bool DrawOwnedMarkers() => DrawMarkersSection();
}
