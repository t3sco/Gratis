using System.Numerics;
using Gratis.Classes;
using Gratis.Rendering;
using Gratis.Windows;
using ImGuiNET;

namespace Gratis.Preview;

internal sealed partial class ScriptedFrameDriver {
    public double StartupMs { get; private set; }
    public double FontRegistrationMs { get; private set; }
    public double FontAtlasMs { get; private set; }
    public bool BenchmarkMapVisible { get; set; } = true;
    public bool BenchmarkSettingsVisible { get; set; }
    public int BenchmarkLabelsDrawn { get; private set; }
    public SettingsLayout BenchmarkSettings { get; } = new();
    public List<(MarkerInfo Marker, string Text, Vector2 World)> BenchmarkLabels { get; } = [];

    private void DrawBenchmarkWindows() {
        if (!BenchmarkSettingsVisible) return;
        using var probe = BenchmarkProbe.Measure(BenchmarkSection.Settings);
        ImGui.SetNextWindowPos(new(160, 50));
        ImGui.SetNextWindowSize(new(820, 720));
        ImGui.SetNextWindowFocus();
        var theme = SettingsTheme.Push(Config);
        if (ImGui.Begin("Gratis settings", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoCollapse))
            BenchmarkSettings.Draw(Config, null);
        ImGui.End();
        theme.Pop();
    }
}
