using System.Diagnostics;
using System.Numerics;
using Gratis.Classes;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class Program {
    private static readonly MarkerInfo[] DenseMarkers = CreateMarkers(600, false, .54f);
    private static readonly MarkerInfo[] ClusterMarkers = CreateMarkers(600, true, .54f);
    private static readonly MarkerInfo[] ZoomedMarkers = CreateMarkers(2000, false, .27f);
    private static readonly string[] MarkerScenes = [
        "markers-dense",
        "markers-clustered",
        "markers-zoomed-out",
        "markers-legend",
        "markers-quests",
        "markers-enemies",
        "players",
        "markers-parchment",
        "markers-dark",
        "markers-fallback"];

    private static bool IsMarkerScene(string scene) => MarkerScenes.Contains(scene);

    private static void ConfigureMarkerPreview(string scene, PreviewContext context, bool? declutter) {
        context.Config.Markers.Declutter = declutter
            ?? (scene is "markers-dense" or "markers-clustered" or "markers-zoomed-out");
        if (scene is "markers-legend" or "markers-quests")
            context.Config.Markers.ScaleWithZoom = true;
        if (scene == "markers-fallback") {
            context.IconFontsEnabled = false;
            context.Config.Markers.IconStyle = MarkerIconStyle.Icons;
            context.Config.Markers.ScaleWithZoom = true;
            context.Config.Markers.MinimumScale = .5f;
            context.Config.Markers.MaximumScale = 2;
            context.Config.Markers.Declutter = false;
        }
        if (scene is "markers-parchment" or "markers-dark") {
            context.Config.Markers.ScaleWithZoom = true;
            context.Config.Markers.MinimumScale = .5f;
            context.Config.Markers.MaximumScale = 2;
            context.Config.Markers.Declutter = true;
        }
    }

    private static MarkerInfo[] CreateMarkers(int count, bool clustered, float zoom) {
        var markers = new MarkerInfo[count];
        uint[] protectedIcons = [71021, 71203, 71145, 60502, 60453, 60430, 60421, 60071];
        for (var i = 0; i < count; i++) {
            var priority = !clustered ? i % 12 < 8 : i < 16;
            var icon = priority ? protectedIcons[i % 8] : i % 2 == 0 ? 61000u : 60412u;
            var label = $"{(priority ? "Protected" : "Ambient")} {i + 1}";
            var unit = clustered ? new Vector2(.5f + MathF.Sin(i * 2.4f) * .013f, .5f + MathF.Cos(i * 1.7f) * .013f)
                : new Vector2(.08f + ((i * 71) % 997) / 997f * .84f, .08f + ((i * 137) % 991) / 991f * .84f);
            if (clustered) {
                if (priority)
                    unit = new Vector2(.15f + i % 8 * .1f, .27f + i / 8 * .15f);
                else if (i < 48)
                    unit = new Vector2(.15f, .27f) + new Vector2(MathF.Sin(i), MathF.Cos(i)) * .003f;
                else
                    unit.Y += .15f;
            }
            markers[i] = new MarkerInfo {
                Position = unit * (2048 * zoom), Offset = Vector2.Zero, Scale = zoom,
                IconId = icon,
                PrimaryText = () => label, IsRelevant = i % 3 != 0,
            };
        }
        return markers;
    }

    private static void DrawMarkerScene(string scene, Vector2 frame) {
        if (scene == "markers-fallback") {
            DrawMarkerFallbackScene(frame);
            return;
        }
        if (scene is "markers-parchment" or "markers-dark") {
            DrawLegibilityScene(scene, frame);
            return;
        }
        if (scene == "markers-enemies") {
            DrawNeutralMarkerScene(frame);
            return;
        }
        if (scene == "players") {
            DrawPlayersScene(frame);
            return;
        }
        if (scene is "markers-legend" or "markers-quests") {
            DrawMarkerLegend(scene, frame);
            return;
        }
        var markers = scene == "markers-dense" ? DenseMarkers : scene == "markers-clustered" ? ClusterMarkers
            : ZoomedMarkers;
        foreach (var marker in markers) {
            marker.Offset = (frame - new Vector2(2048 * marker.Scale)) / 2;
            DrawHelpers.DrawMapMarker(marker);
        }
    }

    private static readonly (string Name, uint Icon)[] QuestSamples = [
        ("Side available", 71021), ("Side progress", 71023), ("Side complete", 71025), ("Side locked", 71031),
        ("MSQ available", 71201), ("MSQ progress", 71203), ("MSQ complete", 71205), ("MSQ locked", 71211),
        ("Blue available", 71141), ("Blue progress", 71143), ("Blue complete", 71145), ("Blue locked", 71151),
        ("Repeat side", 71022), ("Repeat MSQ", 71202), ("Repeat blue", 71142), ("Objective ?", 60071),
    ];

    private static void DrawMarkerLegend(string scene, Vector2 frame) {
        var context = MapDrawContext.Current;
        if (ImGui.GetFrameCount() == 1) {
            foreach (var family in Enum.GetValues<MarkerIconFamily>())
                if (!GratisIcons.Available(MarkerIcon.Glyph(family), 14)
                    || !GratisIcons.Available(MarkerIcon.Glyph(family), 10))
                    throw new InvalidOperationException($"Missing Lucide glyph: {family}.");
        }
        var scale = context.GlobalScale;
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetWindowPos();
        list.AddRectFilled(origin + new Vector2(8, 64) * scale, origin + frame - new Vector2(8, 42) * scale,
            ImGui.GetColorU32(new Vector4(.07f, .10f, .14f, .96f)), 6 * scale);
        using var font = context.GetChromeFont(12 * scale, ChromeFontRole.Body)?.Push();
        DrawBinding.PushFontTexture(list);
        list.AddText(ImGui.GetFont(), 12 * scale, origin + new Vector2(20, 73) * scale, ImGui.GetColorU32(Vector4.One),
            "Contextual icons  /  1.0x + 0.7x zoom  /  14px default");
        var columns = scene == "markers-quests" ? 4 : 6;
        var cellWidth = (frame.X - 36 * scale) / columns;
        var cellHeight = (scene == "markers-quests" ? 110 : 43) * scale;
        var index = 0;
        void Sample(string name, uint id, MarkerIconFamily family = MarkerIconFamily.Auto) {
            var cell = new Vector2(20 * scale + index % columns * cellWidth,
                104 * scale + index / columns * cellHeight);
            list.AddText(ImGui.GetFont(), 11 * scale, origin + cell, ImGui.GetColorU32(GratisTheme.Secondary), name);
            for (var zoom = 0; zoom < 2; zoom++) {
                var marker = new MarkerInfo {
                    Position = cell / scale + new Vector2(12 + zoom * 29, 24),
                    Offset = (cell / scale + new Vector2(12 + zoom * 29, 24)) * (scale - 1),
                    Scale = zoom == 0 ? 1 : .7f, IconId = id, IconFamily = family, PrimaryText = () => name,
                };
                DrawHelpers.DrawMapMarker(marker);
            }
            index++;
        }
        foreach (var (name, id) in QuestSamples)
            Sample(name, id);
        if (scene != "markers-quests") {
            foreach (var family in Enum.GetValues<MarkerIconFamily>()) {
                if (family is MarkerIconFamily.Auto or MarkerIconFamily.Quest)
                    continue;
                Sample(family.ToString(), 60010, family);
            }
        }
        DrawBinding.PopTexture(list);
    }

    private static void VerifyMarkers(PreviewContext context, string scene) {
        var batch = context.Markers;
        if (scene == "markers-fallback") {
            if (context.MarkerRendering.Count(MarkerDrawPath.PlateFallback) != 24
                || context.MarkerRendering.Count(MarkerDrawPath.Shape) != 0)
                throw new InvalidOperationException(
                    "Icons fallback scene must draw 24 contextual plates and zero bare shapes.");
            Console.WriteLine(context.MarkerRendering.Diagnostics());
            return;
        }
        if (scene is "markers-parchment" or "markers-dark") {
            if (batch.Protected < 18 || batch.Clusters < 1)
                throw new InvalidOperationException(
                    "Legibility scene must retain priority markers and a cluster badge.");
            Console.WriteLine("Opaque terrain: priority icons at 0.7x/1x/1.5x, native text sizes, trail and cluster badge.");
            return;
        }
        if (scene == "markers-enemies") {
            MarkerClassificationVerification.Run();
            return;
        }
        if (scene is "markers-legend" or "markers-quests") {
            if (batch.Enabled)
                for (var i = 0; i < QuestSamples.Length * 2; i++)
                    if (!batch.IsStandalone(i))
                        throw new InvalidOperationException("Legend quest was hidden or merged.");
            Console.WriteLine($"Quest samples: {QuestSamples.Length * 2} visible; declutter "
                + $"{(batch.Enabled ? "ON: all standalone" : "OFF: direct rendering")}.");
            return;
        }
        if (!context.Config.Markers.Declutter) {
            if (new MarkerSettings().IconStyle != MarkerIconStyle.Icons)
                throw new InvalidOperationException("Marker reset/default style.");
            Console.WriteLine("Declutter OFF: every marker uses direct rendering.");
            return;
        }
        for (var i = 0; i < batch.Submitted; i++)
            if (MarkerRelevance.IsProtected((scene == "markers-clustered" ? ClusterMarkers : scene == "markers-dense"
                ? DenseMarkers : ZoomedMarkers)[i])
                && !batch.IsStandalone(i))
                throw new InvalidOperationException("Priority marker was hidden or merged.");
        Console.WriteLine($"Drawn batch: {batch.Submitted} submitted; {batch.Protected} protected; "
            + $"{batch.Hidden} hidden; {batch.Clusters} ambient clusters; "
            + $"{batch.DissolvedClusters} dissolved for priority overlap.");
        foreach (var count in new[] { 200, 600, 2000 }) {
            var markers = count == 2000 ? ZoomedMarkers : DenseMarkers;
            void Resolve() {
                batch.Begin();
                for (var i = 0; i < count; i++)
                    batch.Enqueue(markers[i]);
                batch.Resolve();
            }
            for (var warm = 0; warm < 16; warm++)
                Resolve();
            var bytes = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            for (var sample = 0; sample < 100; sample++)
                Resolve();
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds / 100;
            bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
            if (bytes != 0)
                throw new InvalidOperationException($"Marker sort/grid allocated {bytes} bytes.");
            Console.WriteLine($"Markers {count}: sort/grid {elapsed:F3} ms/frame, {bytes} B/100 frames, "
                + $"{batch.DistanceChecks} comparisons.");
        }
        if (MarkerVisual.Alpha(ZoomedMarkers[0]) != 1 || MarkerVisual.Scale(ZoomedMarkers[0]) != 1)
            throw new InvalidOperationException("Protected markers remain opaque; zoom scaling defaults off.");
    }
}