using System.Numerics;
using Gratis.Classes;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class MarkerClassificationVerification
{
    public static void Run()
    {
        var cases = new (string Name, uint Icon, MarkerType Kind, MarkerCategory Expected)[] {
            ("unmapped", 61000, MarkerType.Unknown, MarkerCategory.Poi),
            ("unmapped kind", 61000, (MarkerType)99, MarkerCategory.Poi),
            ("boss engaged", 60401, MarkerType.Unknown, MarkerCategory.Poi),
            ("boss idle", 60402, MarkerType.Unknown, MarkerCategory.Poi),
            ("combatant engaged", 60422, MarkerType.Unknown, MarkerCategory.Poi),
            ("Defective Turret", 60424, MarkerType.Unknown, MarkerCategory.Poi),
            ("NPC unknown marker kind", 60424, (MarkerType)99, MarkerCategory.Poi),
            ("hunt sheet", 63001, MarkerType.Unknown, MarkerCategory.Poi),
            ("treasure", 60003, MarkerType.Unknown, MarkerCategory.Poi),
            ("gathering", 60438, MarkerType.Unknown, MarkerCategory.Poi),
            ("event NPC unknown", 62000, MarkerType.Unknown, MarkerCategory.Poi),
            ("circle only", 60493, MarkerType.Unknown, MarkerCategory.Poi),
            ("blue mage portrait", 72001, MarkerType.Unknown, MarkerCategory.Poi),
            ("NPC portrait", 73001, MarkerType.Unknown, MarkerCategory.Poi),
            ("vendor", 60412, MarkerType.Unknown, MarkerCategory.Service),
            ("transport", 60453, MarkerType.Unknown, MarkerCategory.Transport),
            ("side available", 71021, MarkerType.Unknown, MarkerCategory.Objective),
            ("side complete", 71025, MarkerType.Unknown, MarkerCategory.Objective),
            ("MSQ progress", 71203, MarkerType.Unknown, MarkerCategory.Objective),
            ("feature locked", 71151, MarkerType.Unknown, MarkerCategory.Objective),
            ("repeatable", 71022, MarkerType.Unknown, MarkerCategory.Objective),
            ("FATE", 60502, MarkerType.Fate, MarkerCategory.Objective),
            ("Stellar", 61000, MarkerType.Stellar, MarkerCategory.Objective),
        };
        var saved = MapDrawContext.Current;
        using var context = new PreviewContext(new Textures());
        MapDrawContext.Current = context;
        try {
            context.Config.Markers.DimIrrelevant = true;
            context.Config.Markers.IrrelevantAlpha = .3f;
            foreach (var test in cases) {
                var marker = new MarkerInfo {
                    Position = Vector2.Zero, Offset = Vector2.Zero, Scale = 1, IconId = test.Icon,
                    MarkerType = test.Kind, Category = MarkerCategory.Objective, IsDynamicMarker = true, IsRelevant = false,
                };
                Require(FlatMarkerDrawing.Category(marker) == test.Expected, test.Name);
                if (test.Expected != MarkerCategory.Objective)
                    Require(FlatMarkerDrawing.Label(marker) != "Quest / objective", test.Name + " subtitle");
                if (MarkerIcon.Family(marker) is MarkerIconFamily.Neutral or MarkerIconFamily.Hunt) {
                    Require(!MarkerRelevance.IsProtected(marker) && MarkerVisual.Alpha(marker) == .3f, test.Name + " ambient policy");
                    Require(FlatMarkerDrawing.Label(marker) == "Point of interest", test.Name + " neutral label");
                }
                if (test.Expected == MarkerCategory.Objective)
                    Require(MarkerRelevance.IsProtected(marker) && MarkerVisual.Alpha(marker) == 1, test.Name + " priority policy");
            }
            var flagged = new MarkerInfo { Position = Vector2.Zero, Offset = Vector2.Zero, Scale = 1, IconId = 61000, IsObjective = true };
            Require(FlatMarkerDrawing.Category(flagged) == MarkerCategory.Objective && MarkerRelevance.IsProtected(flagged), "Explicit game objective");
            var npc = new MarkerInfo { Position = Vector2.Zero, Offset = Vector2.Zero, Scale = 1, IconId = 60424, IsTarget = true };
            Require(FlatMarkerDrawing.Category(npc) == MarkerCategory.Poi && MarkerRelevance.IsProtected(npc), "A target is protected without becoming a quest");
            Console.WriteLine($"Marker classification verified: {cases.Length} icon/kind fixtures plus explicit objective and target; non-quests never inherit Objective, hostile/unknown markers remain neutral ambient POIs.");
        }
        finally { MapDrawContext.Current = saved; }
    }

    private static void Require(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("Marker classification: " + name);
    }
}
