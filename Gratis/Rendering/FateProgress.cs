using System;
using System.Globalization;
using System.Numerics;
using Gratis.Classes;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public enum FateProgressState { Unknown, Preparing, Active, Complete, Failed, WaitingForEnd }
public readonly record struct FateSnapshot(FateProgressState State, int Progress, long RemainingSeconds, int Duration,
    bool HasBonus = false);
public readonly record struct FateDisplay(FateProgressState State, float Fraction, string Text, bool HasBonus = false);

public static class FateProgress {
    // Called at snapshot refresh, never from the per-marker drawing loop.
    public static FateDisplay Evaluate(FateSnapshot snapshot) {
        var progress = Math.Clamp(snapshot.Progress, 0, 100);
        // A bonus only matters while the FATE can still be joined or finished.
        var bonus = snapshot.HasBonus && snapshot.State is FateProgressState.Preparing or FateProgressState.Active;
        var text = snapshot.State switch {
            FateProgressState.Preparing => "Preparing",
            FateProgressState.Complete => "Complete",
            FateProgressState.Failed => "Failed",
            FateProgressState.WaitingForEnd => "Waiting for end",
            FateProgressState.Active => "Progress: " + progress.ToString(CultureInfo.InvariantCulture) + "%\nTime left: "
                + (snapshot.Duration > 0 ? FormatTime(Math.Clamp(snapshot.RemainingSeconds, 0, snapshot.Duration)) : "Unavailable"),
            _ => "Status unavailable",
        };
        return new(snapshot.State, progress / 100f, bonus ? BonusText + "\n" + text : text, bonus);
    }

    public const string BonusText = "EXP bonus";

    public static string FormatTime(long seconds) {
        seconds = Math.Max(0, seconds);
        return (seconds / 60).ToString(CultureInfo.InvariantCulture) + ":"
            + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
    }

    public static void Draw(MarkerInfo marker) {
        var context = MapDrawContext.Current;
        if (!context.Config.IsGratis) return;
        // Flat markers draw their own badge with the hover-scaled radius; game icons get it here.
        if (!MarkerVisual.UseFlatMarker(marker))
            FateBonusBadge.DrawOnIcon(marker);
        if (!context.Config.Markers.ShowFateProgress
            || marker.FateProgress is not { State: FateProgressState.Active } progress)
            return;
        var half = MarkerVisual.HalfSize(marker);
        var center = marker.Position + marker.Offset + ImGui.GetWindowPos();
        var scale = context.GlobalScale;
        // Inside the existing protected marker footprint: no new collisions or hover target.
        var min = center + new Vector2(-half.X * .7f, half.Y - 3 * scale);
        var max = center + new Vector2(half.X * .7f, half.Y);
        if (!MapShape.ContainsSymbol(center, half)) return;
        var list = ImGui.GetWindowDrawList();
        list.AddRectFilled(min, max, ImGui.GetColorU32(GratisTheme.Surface));
        if (progress.Fraction > 0)
            list.AddRectFilled(min, new Vector2(min.X + (max.X - min.X) * progress.Fraction, max.Y),
                ImGui.GetColorU32(FlatMarkerDrawing.Color(FlatMarkerDrawing.Category(marker))));
    }
}
