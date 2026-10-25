using System.Numerics;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class Program {
    private static double previewPingStarted;
    private static readonly string[] MotionScenes = ["motion-ping", "motion-trail", "motion-zone", "motion-reduced"];
    private static bool IsMotionScene(string scene) =>
        scene is "motion-ping" or "motion-trail" or "motion-zone" or "motion-reduced";

    private static bool DrawMotionMap(string scene, Vector2 frame, DrawTexture texture) {
        if (!IsMotionScene(scene))
            return false;
        var motion = MapDrawContext.Current.Motion;
        var now = ImGui.GetTime();
        motion.BeginMap(scene == "motion-zone" && ImGui.GetFrameCount() >= 8 ? 2u : 1u, now);
        var scale = Math.Max(frame.X / texture.Size.X, frame.Y / texture.Size.Y) * 1.15f;
        var progress = motion.ZoneProgress(now);
        var center = ViewportMath.ClampCenter(texture.Size / 2 - new Vector2((1 - progress) * 8 / scale, 0), frame,
            texture.Size, scale, false);
        ImGui.SetCursorPos(frame / 2 - center * scale);
        using var alpha = new MapMotion.AlphaScope(progress);
        MapTextureDrawing.DrawMapImage(texture, scale);
        return true;
    }

    private static void DrawMotionTrail(string scene, Vector2 frame, Vector2 player) {
        if (scene is not ("motion-trail" or "motion-reduced"))
            return;
        var motion = MapDrawContext.Current.Motion;
        motion.Trail.Clear();
        var now = ImGui.GetTime();
        var local = player - ImGui.GetWindowPos();
        for (var i = 0; i <= 80; i++) {
            var t = i / 80f;
            var p = local - new Vector2((1 - t) * frame.X * .22f, MathF.Sin((1 - t) * 2.6f) * frame.Y * .13f);
            motion.Trail.Add(p, now - (1 - t) * 4, 4);
        }
        motion.DrawTrail(ImGui.GetWindowPos(), 1, now);
    }

    private static void DrawMotionPings(string scene, Vector2 frame) {
        if (scene is not ("motion-ping" or "motion-reduced"))
            return;
        var now = ImGui.GetTime();
        if (ImGui.GetFrameCount() == 1) {
            previewPingStarted = now - .025;
            MapDrawContext.Current.Motion.PingAt(frame * new Vector2(.56f, .53f), previewPingStarted);
        }
        MapDrawContext.Current.Motion.DrawPings(ImGui.GetWindowPos(), 1, now);
    }

    private static void ReportMotion(string scene, PreviewContext context) {
        if (scene == "motion-ping") {
            var age = ImGui.GetTime() - previewPingStarted;
            if (Math.Abs(age - .3) > 1e-6)
                throw new InvalidOperationException("Ping preview must capture t=300ms.");
            Console.WriteLine($"Ping capture: {age * 1000:F0}ms, two accent rings.");
        }
        Console.WriteLine($"Motion enabled: {MapMotion.Enabled}; trail samples: "
            + $"{context.Motion.Trail.Count}; zone progress: {context.Motion.ZoneProgress(ImGui.GetTime()):F3}.");
    }
}