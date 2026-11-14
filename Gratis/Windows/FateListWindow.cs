using System;
using System.Drawing;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Gratis.Compat.Window;
using Gratis.Data;
using Gratis.Extensions;
using Gratis.Rendering;

namespace Gratis.Windows;

/// <summary>
/// The FATE list for the Classic look. In Gratis mode the list lives on the map (the FATE button beside the quest
/// log), so opening this window from a command, the settings page or the old menu opens that panel instead.
/// </summary>
public class FateListWindow : Window {
    private static float ElementHeight => 48.0f * ImGuiHelpers.GlobalScale;

    public FateListWindow() : base("Gratis Fate List Window", new Vector2(300.0f, 400.0f)) {
    }

    public override void Update() {
        base.Update();
        if (!System.SystemConfig.IsGratis)
            return;
        System.IntegrationsController.OpenOccupiedMap();
        MapDrawContext.Current.Fates.RequestOpen();
        IsOpen = false;
    }

    public override bool DrawConditions() => !System.SystemConfig.IsGratis;

    protected override void DrawContents() {
        var source = System.MapWindow.FateList;
        DrawOptions();
        var reason = source.Unavailable;
        if (reason.Length > 0) {
            DrawEmpty(reason);
            return;
        }
        var fates = FateList.Sort(source.Snapshot.Fates);
        if (fates.Length == 0) {
            DrawEmpty(FateList.EmptyText);
            return;
        }
        var now = global::System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var fate in fates) {
            var cursorStart = ImGui.GetCursorScreenPos();
            if (ImGui.Selectable($"##{fate.Id}_Selectable", false, ImGuiSelectableFlags.None,
                new Vector2(ImGui.GetContentRegionAvail().X, ElementHeight)))
                source.Centre(fate);
            ImGui.SetCursorScreenPos(cursorStart);
            DrawFateInfo(fate, now);
        }
    }

    private static void DrawEmpty(string text) {
        var textSize = ImGui.CalcTextSize(text);
        ImGui.SetCursorPosX(MathF.Max(0, ImGui.GetContentRegionAvail().X / 2.0f - textSize.X / 2.0f));
        ImGui.SetCursorPosY(ImGui.GetContentRegionAvail().Y / 2.0f - textSize.Y / 2.0f);
        ImGui.TextColored(KnownColor.Orange.Vector(), text);
    }

    private static void DrawOptions() {
        using var toolbarChild = ImRaii.Child("fatelist_toolbar",
            new Vector2(ImGui.GetContentRegionAvail().X, 32.0f * ImGuiHelpers.GlobalScale));
        if (toolbarChild) {
            ImGui.Spacing();
            if (ImGui.Checkbox("Place Map Flag on Click", ref System.SystemConfig.SetFlagOnFateClick))
                SystemConfig.Save();
        }
        ImGui.Separator();
    }

    private static void DrawFateInfo(FateListEntry fate, long now) {
        using (ImRaii.Child($"image_child_{fate.Id}", new Vector2(ElementHeight, ElementHeight), false,
            ImGuiWindowFlags.NoInputs)) {
            ImGui.Image(Service.TextureProvider.GetFromGameIcon(new GameIconLookup { IconId = fate.IconId })
                .GetWrapOrEmpty().Handle, new Vector2(ElementHeight, ElementHeight));
        }
        ImGui.SameLine();
        using (ImRaii.Child($"text_child_{fate.Id}", new Vector2(ImGui.GetContentRegionAvail().X, ElementHeight),
            false, ImGuiWindowFlags.NoInputs)) {
            ImGui.TextUnformatted($"Lv. {fate.Level} {fate.Name}");
            ImGui.TextUnformatted(FateList.StateText(fate));
            var trailing = (fate.Bonus ? "Exp Bonus!   " : "") + FateList.Trailing(fate, now);
            if (trailing.Length > 0) {
                ImGui.SameLine(ImGui.GetContentRegionMax().X - ImGui.CalcTextSize(trailing).X);
                ImGui.TextUnformatted(trailing);
            }
        }
    }

    public override void OnClose() {
        System.WindowManager.RemoveWindow(this);
    }
}
