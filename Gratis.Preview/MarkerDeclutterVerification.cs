using System.Numerics;
using System.Text.Json;
using Gratis.Classes;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class MarkerDeclutterVerification {
    public static void Run() {
        var saved = MapDrawContext.Current;
        using var context = new PreviewContext(new Textures());
        MapDrawContext.Current = context;
        try {
            VerifyDefaultsAndLegacy(context);
            var random = new Random(0x47524154);
            var totalProtected = 0;
            var totalClusters = 0;
            for (var layout = 0; layout < 1000; layout++) {
                context.DpiScale = layout % 2 + 1;
                context.Config.Markers.IconStyle = (MarkerIconStyle)(layout % 3);
                context.Config.Markers.Declutter = true;
                context.Config.Markers.ScaleWithZoom = layout % 2 == 0;
                context.Config.Markers.DimIrrelevant = true;
                context.Config.Markers.IrrelevantAlpha = .1f;
                context.Config.Markers.ClusterDistance = random.Next(8, 65);
                var batch = context.Markers;
                batch.Begin();
                var count = random.Next(64, 257);
                var samples = new MarkerInfo[count];
                var positions = new Vector2[count];
                var offsets = new Vector2[count];
                var expectedProtected = new bool[count];
                var width = layout % 3 == 0 ? 48 : layout % 3 == 1 ? 512 : 4096;
                for (var i = 0; i < count; i++) {
                    var kind = random.Next(20);
                    var icon = kind switch {
                        0 => 71021u,
                        1 => 71203u,
                        2 => 71145u,
                        3 => 71151u,
                        4 => 60502u,
                        5 => 60453u,
                        6 => 60430u,
                        7 => 60421u,
                        >= 14 when kind % 2 == 0 => 60412u,
                        _ => 61000u,
                    };
                    var position = new Vector2(random.NextSingle() * width, random.NextSingle() * width * .7f);
                    var offset = new Vector2(random.Next(-16, 17), random.Next(-16, 17));
                    var marker = new MarkerInfo {
                        Position = position, Offset = offset, Scale = .15f + random.NextSingle() * 2,
                        IconId = icon, IsRelevant = false,
                        IsFlag = kind == 8, IsTarget = kind == 9, IsTracked = kind == 10, IsHovered = kind == 11,
                        ObjectiveId = kind == 12 ? 42u : null,
                        IsDynamicMarker = kind == 13,
                        IsObjective = kind is 12 or 13,
                        MarkerType = kind == 4 ? MarkerType.Fate : MarkerType.Unknown,
                    };
                    samples[i] = marker;
                    positions[i] = position;
                    offsets[i] = offset;
                    expectedProtected[i] = kind < 14;
                    Require(MarkerRelevance.IsProtected(marker) == expectedProtected[i],
                        "Fixture protection classification");
                    Require(batch.Enqueue(marker), "Declutter ON accepts every marker");
                }
                batch.Resolve();
                var represented = 0;
                for (var i = 0; i < count; i++) {
                    var marker = samples[i];
                    Require(marker.Position == positions[i] && marker.Offset == offsets[i], "No marker may be nudged");
                    var root = batch.RootOf(i);
                    Require(root >= 0, "No density hiding");
                    if (expectedProtected[i]) {
                        totalProtected++;
                        Require(batch.IsStandalone(i), "Priority must never hide or merge");
                        Require(MarkerVisual.Alpha(marker) == 1, "Priority must remain fully opaque");
                    }
                    if (root != i) {
                        Require(!expectedProtected[i] && !expectedProtected[root],
                            "Clusters contain ambient markers only");
                        Require(FlatMarkerDrawing.Category(marker) == FlatMarkerDrawing.Category(samples[root]),
                            "Cluster categories must match");
                    }
                    else {
                        represented += batch.CountAt(i);
                        if (batch.CountAt(i) > 1) {
                            totalClusters++;
                            Require(!batch.ClusterOverlapsProtected(i),
                                "Cluster badge must not overlap any priority marker");
                        }
                    }
                }
                Require(represented == count && batch.Hidden == 0, "Every marker is represented exactly once");
                context.Config.Markers.Declutter = false;
                batch.Begin();
                foreach (var marker in samples)
                    Require(!batch.Enqueue(marker), "Declutter OFF uses direct rendering");
            }
            Require(totalClusters > 0, "Randomized cases must exercise ambient clustering");
            Console.WriteLine($"Markers verified: 1,000 random layouts, {totalProtected:N0} protected markers, "
                + $"{totalClusters:N0} ambient clusters; no hidden/merged/dimmed/nudged priority "
                + "markers or overlapping badges; ON/OFF and 1x/2x DPI.");
        }
        finally { MapDrawContext.Current = saved; }
    }

    private static void VerifyDefaultsAndLegacy(PreviewContext context) {
        var defaults = new MarkerSettings();
        Require(defaults.Declutter && defaults.ScaleWithZoom && defaults.IconStyle == MarkerIconStyle.Game,
            "Declutter and zoom scaling default on with game icons");
        const string legacy = """{"PlayerIconScale":0.35,"Markers":{"Declutter":true,"HideLowPriority":true,"Priority":[2,0,1],"MinimumScale":0.55}}""";
        var loaded = JsonSerializer.Deserialize<MapDrawingConfig>(legacy,
            new JsonSerializerOptions { IncludeFields = true })!;
        Require(loaded.PlayerIconScale == .35f && loaded.Markers.Declutter && loaded.Markers.MinimumScale == .55f
            && loaded.Markers.IconStyle == MarkerIconStyle.Game,
            "Old settings load; retired fields are ignored");
        context.Config.Markers = defaults;
        var marker = new MarkerInfo { Position = Vector2.Zero, Offset = Vector2.Zero, Scale = .2f, IconId = 71021 };
        Require(MarkerVisual.Scale(marker) == 1, "Gratis zoom scaling defaults clamp to unity independently of Classic setting");
        context.Config.VisualStyle = 1;
        Require(MarkerVisual.Scale(marker) == .2f && MarkerVisual.Alpha(marker) == 1,
            "Classic scale and opacity unchanged");
        context.Config.Markers.Declutter = true;
        context.Markers.Begin();
        Require(!context.Markers.Enabled && !context.Markers.Enqueue(marker), "Classic bypasses the batch");
        context.Config.VisualStyle = 0;
    }

    private static void Require(bool result, string message) {
        if (!result)
            throw new InvalidOperationException(message);
    }
}