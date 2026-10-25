using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Classes;
using Gratis.Data;
using Math = global::System.Math;

namespace Gratis.Rendering;

internal sealed partial class DalamudMapDrawContext : MapDrawContext, IDisposable {
    private bool loggedBlurSuccess;

    private bool loggedBlurFailure;

    public override bool IsPvP => Service.ClientState.IsPvP;

    public override void OpenFilterSettings() => System.ConfigWindow.OpenSection("Filters");

    public override bool TryBlurBehind(ImDrawListPtr list, Vector2 min, Vector2 max, float rounding, float radius,
        float opacity = 1, bool prepend = false) {
        var frame = ImGui.GetFrameCount();
        if (!Blur.CanAttempt(frame) || !float.IsFinite(radius) || radius <= 0 || !float.IsFinite(opacity)
            || opacity < 1 / 255f)
            return false;
        var strength = BlurDiagnostics.Strength(radius, Math.Clamp(opacity, 0, 1));
        Blur.Attempt(strength);
        try {
            AddHostBlur(list, min, max, rounding, strength, prepend);
            Blur.Succeeded();
            if (!loggedBlurSuccess) {
                loggedBlurSuccess = true;
                Service.Log.Information(
                    $"[Gratis] First blur callback recorded (strength {strength:0.##}); /gratis glass "
                    + "reports recording status, not GPU completion.");
            }
            return true;
        }
        catch (Exception error) {
            Blur.Failed(error, frame);
            if (!loggedBlurFailure) {
                loggedBlurFailure = true;
                Service.Log.Information("[Gratis] First blur recording failure; using tinted glass and retrying after "
                    + $"120 frames: {Blur.LastException}");
            }
            return false;
        }
    }

    [global::System.Runtime.CompilerServices.MethodImpl(
        global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void AddHostBlur(ImDrawListPtr list, Vector2 min, Vector2 max, float rounding, float strength,
        bool prepend) {
        if (prepend)
            ImGuiHelpers.PrependBlurBehind(list, min, max, strength, rounding, Vector4.Zero, Vector4.Zero, 0);
        else
            ImGuiHelpers.AddBlurBehind(list, min, max, strength, rounding, Vector4.Zero, Vector4.Zero, 0);
    }

    public override MapDrawingConfig Config => System.SystemConfig;

    public override void SaveConfig() => Data.SystemConfig.Save();

    public override bool LensInDuty =>
        Service.Condition[global::Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty];

    public override bool LensGatheringClass => Service.ObjectTable.LocalPlayer?.ClassJob.RowId is 16 or 17 or 18;

    public override bool LensNearNode {
        get {
            if (IsPvP || Service.ObjectTable.LocalPlayer is not { } player)
                return false;
            foreach (var obj in Service.ObjectTable)
                if (obj.ObjectKind == global::Dalamud.Game.ClientState.Objects.Enums.ObjectKind.GatheringPoint
                    && Vector3.DistanceSquared(player.Position, obj.Position) < 900)
                    return true;
            return false;
        }
    }

    public override MapColourPreset ActiveColourPreset => System.SystemConfig.MapColour?.Preset
        ?? MapColourPreset.Original;

    public override void ZoomCluster(Vector2 position, float scale) =>
        System.MapRenderer.ZoomToCluster(position, scale);

    public override float GlobalScale => ImGuiHelpers.GlobalScale;

    public override unsafe float MapSizeFactor => AgentMap.Instance()->SelectedMapSizeFactorFloat;

    public override DrawTexture GetIcon(uint id) {
        var texture = Service.TextureProvider.GetFromGameIcon(id).GetWrapOrEmpty();
        return new DrawTexture(texture.Handle, texture.Size);
    }

    public override bool PrepareMarker(MarkerInfo marker) {
        if (Config.IsGratis && Config.AetheryteAttunementStyle)
            marker.AetheryteAttuned = System.MapWindow.PlacePalette.MarkerAttunement(marker.DataId ?? 0,
                MarkerIcon.Family(marker));
        if (marker.IsDynamicMarker) {
            foreach (var module in System.Modules) {
                if (module.ProcessMarker(marker))
                    break;
            }
        }
        return true;
    }

    public override bool IsRadiusHovered(Vector2 center, float radius) =>
        Vector2.Distance(ImGui.GetMousePos() - System.MapWindow.MapDrawOffset + ImGui.GetWindowPos(), center) <= radius
        && System.MapWindow.HoveredFlags.Any();

    public void Dispose() {
        classicAxis?.Dispose();
        foreach (var font in titleFonts.Values)
            font.Dispose();
        titleFonts.Clear();
        textDrawFonts.Clear();
        foreach (var font in iconFonts.Values)
            font.Dispose();
        iconFonts.Clear();
        iconDrawFonts.Clear();
    }

    private static FontAwesomeIcon Icon(ToolbarAction action) => action switch {
        ToolbarAction.Parent => FontAwesomeIcon.ArrowUp,
        ToolbarAction.Layers => FontAwesomeIcon.LayerGroup,
        ToolbarAction.Follow => FontAwesomeIcon.LocationArrow,
        ToolbarAction.CenterPlayer => FontAwesomeIcon.ArrowsToCircle,
        ToolbarAction.CenterMap => FontAwesomeIcon.MapMarked,
        ToolbarAction.Search => FontAwesomeIcon.Search,
        ToolbarAction.Settings => FontAwesomeIcon.Cog,
        ToolbarAction.Close => FontAwesomeIcon.Times,
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    public override bool IconButton(ToolbarAction action, string id, string tooltip) =>
        GratisGuiTweaks.IconButton(Icon(action), id, tooltip);

    public override float SettingsButtonWidth {
        get {
            using var font = Service.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push();
            return ImGui.CalcTextSize(FontAwesomeIcon.Cog.ToIconString()).X + ImGui.GetStyle().FramePadding.X * 2.0f;
        }
    }
}