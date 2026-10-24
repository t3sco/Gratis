using System.Numerics;
using System.Text.Json;
using Gratis.Classes;
using Gratis.Data;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class MarkerVisibilityVerification
{
    internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    public static void Run()
    {
        var random = new Random(41415);
        var states = Enum.GetValues<MarkerVisibilityState>();
        for (var mix = 0; mix < 1000; mix++) {
            var group = (MarkerGroup)random.Next(MarkerVisibility.GroupCount);
            foreach (var lens in Lenses.BuiltIn)
                foreach (var filter in states) {
                    var effective = MarkerVisibility.Resolve(lens.State(group), filter, true, false);
                    Require(effective != MarkerVisibilityState.Hidden || filter == MarkerVisibilityState.Hidden, "A lens/solo quiet state hid priority");
                    Require(MarkerVisibility.Resolve(lens.State(group), null, true, false) != MarkerVisibilityState.Hidden, "Lens hid priority");
                    Require(MarkerVisibility.Resolve(lens.State(group), filter, true, true) == MarkerVisibilityState.Normal, "Peek did not reveal");
                }
        }
        foreach (var family in Enum.GetValues<MarkerIconFamily>()) {
            var marker = new MarkerInfo { Position = Vector2.Zero, Offset = Vector2.Zero, Scale = 1, IconFamily = family };
            Require((int)MarkerVisibility.Group(marker) < MarkerVisibility.GroupCount, "Unmapped family");
        }
        foreach (var id in new uint[] {71001,71005,71011,71021,71023,71025,71031,71141,71143,71145,71151,60071,60453,60452,60421,60442,60501}) {
            var marker = new MarkerInfo { Position = Vector2.Zero, Offset = Vector2.Zero, Scale = 1, IconId = id };
            Require(MarkerVisibility.IsPriority(marker), $"Priority icon {id} missing");
        }
        var config = new MapDrawingConfig();
        var model = new MarkerVisibility();
        model.Advance(config, 0, false);
        model.Toggle(MarkerGroup.Services);
        model.Advance(config, .01, false);
        var previous = MapDrawContext.Current;
        using var context = new PreviewContext(new Textures(), config);
        MapDrawContext.Current = context;
        try {
            var marker = new MarkerInfo { Position = Vector2.Zero, Offset = Vector2.Zero, Scale = 1, IconId = 60412 };
            context.Visibility.Advance(config, 0, false);
            context.Visibility.Toggle(MarkerGroup.Services);
            context.Visibility.Advance(config, .01, false);
            context.Visibility.Advance(config, .085, false);
            Require(MathF.Abs(context.Visibility.Visual(marker).X - .5f) < .001f, "150 ms midpoint");
            context.Visibility.Advance(config, .16, false);
            Require(context.Visibility.Visual(marker).X == 0, "Hidden transition endpoint");
            context.Visibility.Advance(config, .17, true);
            context.Visibility.Advance(config, .32, true);
            Require(context.Visibility.Visual(marker) == Vector2.One, "Peek endpoint");
            context.Visibility.Advance(config, .33, false);
            context.Visibility.Advance(config, .49, false);
            Require(context.Visibility.Visual(marker).X == 0, "Peek failed to restore");
            config.ReduceMotion = true;
            context.Visibility.Emphasize(MarkerGroup.Services);
            context.Visibility.Advance(config, .50, false);
            Require(context.Visibility.Visual(marker) == MarkerVisibility.Presentation(MarkerVisibilityState.Loud), "ReduceMotion must snap");
            config.VisualStyle = 1;
            Require(MarkerVisual.Alpha(marker) == 1 && context.Visibility.Visual(marker) == Vector2.One, "Classic changed");
        }
        finally { MapDrawContext.Current = previous; }
        var json = new JsonSerializerOptions { IncludeFields = true };
        var old = JsonSerializer.Deserialize<MapDrawingConfig>("{\"IconScale\":0.7,\"Markers\":{\"Declutter\":false}}", json)!;
        Require(old.ActiveLens == "Explore" && !old.LensAuto && !old.LensTintsMap && old.CustomLenses.Count == 0 && old.IconScale == .7f, "Legacy config defaults");
        old.CustomLenses.Add(new Lens { Name = "Saved", States = Lens.CreateStates(MarkerVisibilityState.Quiet) });
        old.ActiveLens = "Saved";
        var restored = JsonSerializer.Deserialize<MapDrawingConfig>(JsonSerializer.Serialize(old, json), json)!;
        Require(restored.ActiveLens == "Saved" && restored.CustomLenses[0].State(MarkerGroup.Quests) == MarkerVisibilityState.Quiet, "Custom round trip");
        Require(Lenses.Suggest(true, true, true) == "Minimal" && Lenses.Suggest(false, true, true) == "Gather" && Lenses.Suggest(false, false, true) == "Explore", "Auto context precedence");
        Console.WriteLine("Visibility: 1,000 category mixes × all lenses/states, priority families, transitions, peek restoration, ReduceMotion, Classic, custom/legacy JSON and Auto verified.");
    }
}
