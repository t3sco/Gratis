using System.Diagnostics;
using System.Numerics;
using Gratis.Classes;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static partial class Program
{
    private static readonly MarkerInfo[] DenseMarkers = CreateMarkers(600, false, .54f);
    private static readonly MarkerInfo[] ClusterMarkers = CreateMarkers(600, true, .54f);
    private static readonly MarkerInfo[] ZoomedMarkers = CreateMarkers(2000, false, .27f);
    private static readonly string[] MarkerScenes = ["markers-dense", "markers-clustered", "markers-zoomed-out"];

    private static bool IsMarkerScene(string scene) => scene is "markers-dense" or "markers-clustered" or "markers-zoomed-out";

    private static MarkerInfo[] CreateMarkers(int count, bool clustered, float zoom)
    {
        var markers = new MarkerInfo[count];
        for (var i = 0; i < count; i++) {
            var label = $"{(MarkerCategory)(i % 6 + 1)} {i + 1}";
            var unit = clustered ? new Vector2(.5f + MathF.Sin(i * 2.4f) * .013f, .5f + MathF.Cos(i * 1.7f) * .013f) :
                new Vector2(.08f + ((i * 71) % 997) / 997f * .84f, .08f + ((i * 137) % 991) / 991f * .84f);
            if (clustered && i == 2) unit = new Vector2(.5f);
            markers[i] = new MarkerInfo {
                Position = unit * (2048 * zoom), Offset = Vector2.Zero, Scale = zoom,
                IconId = 60071, Category = (MarkerCategory)(i % 6 + 1),
                PrimaryText = () => label, IsRelevant = i % 3 != 0,
            };
        }
        return markers;
    }

    private static void DrawMarkerScene(string scene, Vector2 frame)
    {
        var markers = scene == "markers-dense" ? DenseMarkers : scene == "markers-clustered" ? ClusterMarkers : ZoomedMarkers;
        foreach (var marker in markers) {
            marker.Offset = (frame - new Vector2(2048 * marker.Scale)) / 2;
            DrawHelpers.DrawMapMarker(marker);
        }
    }

    private static void VerifyMarkers(PreviewContext context)
    {
        var batch = context.Markers;
        Console.WriteLine($"Drawn batch: {batch.Submitted} submitted; {batch.Hidden} density-hidden; {batch.Representatives} representatives; {batch.Clusters} clusters; {batch.DistanceChecks} distance checks.");
        foreach (var count in new[] { 200, 600, 2000 }) {
            var markers = count == 2000 ? ZoomedMarkers : DenseMarkers;
            void Resolve()
            {
                batch.Begin();
                for (var i = 0; i < count; i++) batch.Enqueue(markers[i]);
                batch.Resolve(new Vector2(960, 640));
            }
            for (var warm = 0; warm < 16; warm++) Resolve();
            var bytes = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            for (var sample = 0; sample < 100; sample++) Resolve();
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds / 100;
            bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
            if (bytes != 0) throw new InvalidOperationException($"Marker sort/grid allocated {bytes} bytes.");
            Console.WriteLine($"Markers {count}: sort/grid {elapsed:F3} ms/frame, {bytes} B/100 frames, {batch.DistanceChecks} comparisons.");
        }
        if (MarkerVisual.Alpha(ZoomedMarkers[0]) != .6f || MarkerVisual.Scale(ZoomedMarkers[0]) != .7f)
            throw new InvalidOperationException("Marker dimming / minimum scale defaults.");
    }
}
