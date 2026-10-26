using System.Numerics;
using Gratis.Controllers;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class PaletteQuestVerification {
    private static void Require(bool value, string message) {
        if (!value)
            throw new InvalidOperationException(message);
    }

    public static void Frames() {
        QuestBucket[] sections = [QuestBucket.Scenario, QuestBucket.Scenario, QuestBucket.Chronicles,
            QuestBucket.SideQuests, QuestBucket.AlliedSocieties, QuestBucket.AlliedSocieties, QuestBucket.JobClass,
            QuestBucket.Other];
        for (uint i = 0; i < sections.Length; i++)
            Require(QuestBuckets.FromSection(i) == sections[i], $"Journal section {i}");
        foreach (uint? section in new uint?[] { null, 8, 9, 255, uint.MaxValue })
            Require(QuestBuckets.FromSection(section) == QuestBucket.Other, "Unresolved sections use Other");

        using (var driver = new ScriptedFrameDriver(PaletteVerification.Config(), new FrameScript { Palette = true })) {
            driver.PlaceSource!.AdditionalPlaces.AddRange(PaletteFixture.QuestPlaces);
            var overlay = driver.Palette!;
            var time = 0d;
            void Step(ImGuiKey[]? keys = null, string? text = null, bool control = false) =>
                driver.Step(new(time += .02, Keys: keys, Text: text, Control: control, Mouse: new(-1000)));
            void Click(int chip) {
                var bounds = overlay.QuestChipBounds[chip];
                Require(bounds.Z > bounds.X && bounds.W > bounds.Y, $"Quest chip {chip} is visible");
                var center = new Vector2((bounds.X + bounds.Z) / 2, (bounds.Y + bounds.W) / 2);
                driver.Step(new(time += .02, Mouse: center, Left: false));
                driver.Step(new(time += .02, Mouse: center, Left: true));
                driver.Step(new(time += .02, Mouse: center, Left: false));
                Step();
            }
            Step();
            Step();
            overlay.Open();
            Step();
            for (var i = 0; i < 3; i++) {
                Step([ImGuiKey.Tab]);
                Step();
            }
            var quests = driver.PlaceSource.Entries.Where(p => p.Kind == PlaceKind.Quest).ToArray();
            Require(overlay.Query == "" && overlay.SelectedQuestBucket == null
                && overlay.Results.Count == quests.Length, "Quests browse defaults to All");
            QuestBucket?[] buckets = [null, QuestBucket.Scenario, QuestBucket.SideQuests, QuestBucket.JobClass,
                QuestBucket.Chronicles, QuestBucket.AlliedSocieties, QuestBucket.Other];
            var counts = overlay.QuestCounts.ToArray();
            for (var i = 1; i < buckets.Length; i++) {
                Click(i);
                var expected = quests.Where(p => p.QuestBucket == buckets[i]).Select(p => p.Id).Order().ToArray();
                Require(overlay.SelectedQuestBucket == buckets[i]
                    && overlay.Results.Select(r => r.Place.Id).Order().SequenceEqual(expected),
                    $"Bucket {buckets[i]} filters empty-query browse exclusively: selected={overlay.SelectedQuestBucket}, "
                        + $"actual={string.Join(',', overlay.Results.Select(r => r.Place.Id))}");
                Require(overlay.QuestCounts.SequenceEqual(counts)
                    && overlay.Expansions.Sum(e => e.Places) == expected.Length,
                    "Bucket counts remain stable; expansion counts respect the bucket");
            }
            Click(3);
            Require(overlay.Results.Count == 2 && overlay.Results.All(r => r.Place.QuestRowId == 70371),
                "Multiple destinations and ordinary quest icons retain Job & class classification");
            Step([ImGuiKey.Enter]);
            Step();
            Require(overlay.BrowsedExpansion == 5, "Filtered expansion keyboard browse");
            Step([ImGuiKey.RightArrow]);
            Step();
            var zones = PaletteBrowser.Zones(overlay.Results, driver.PlaceSource.Entries);
            Require(zones.Length == 1 && zones[0].Places.Length == 2, "Zone browse contains only the bucket");
            Click(1);
            Require(overlay.BrowsedExpansion == 5 && overlay.BrowserSelection == 0
                && overlay.Results.Count == 1 && overlay.Results[0].Place.Id == "q:mountains",
                "Bucket change refreshes a drilled expansion and resets browser selection");

            overlay.Open("q:");
            Step();
            Step();
            Require(overlay.SelectedQuestBucket == null && overlay.Results.Count == quests.Length + 1,
                "Reopen resets the bucket; q: includes the main-story action");
            Click(1);
            Require(overlay.Results.Any(r => r.Place.Id == MainStoryObjective.Action.Id
                && r.Place.QuestBucket == QuestBucket.Scenario && r.Place.QuestRowId == 0),
                "Synthetic main-story action belongs to Scenario without a Quest row");
            Click(2);
            Step();
            Step([ImGuiKey.A], control: true);
            Step();
            Step(text: "q:cheese");
            Step();
            Require(overlay.Query == "q:cheese" && overlay.Results.Count == 1
                && overlay.Results[0].Place.Id == "q:cheese", $"Typed q: query preserves the bucket: {overlay.Query}");
            Require(overlay.QuestChipBounds[1] == Vector4.Zero && overlay.QuestCounts[2] == 1,
                "Empty buckets are hidden and matching destinations are counted");
            Click(0);
            Step([ImGuiKey.Tab]);
            Step();
            Require(overlay.Query == "cheese" && overlay.SelectedQuestBucket == null
                && overlay.QuestChipBounds.All(b => b == Vector4.Zero), "Primary tabs clear and hide quest filters");
            overlay.Open("q:no-such-quest-zzzz");
            Step();
            Step();
            Require(overlay.Results.Count == 0 && overlay.QuestCounts.All(c => c == 0)
                && overlay.QuestChipBounds[0].Z > overlay.QuestChipBounds[0].X
                && overlay.QuestChipBounds.Skip(1).All(b => b == Vector4.Zero), "Empty query results retain All reset");
        }

        foreach (var scale in new[] { .8f, 1f, 1.6f }) {
            var config = PaletteVerification.Config();
            config.WindowSize = new(330, 480);
            config.ChromeScale = scale;
            using var driver = new ScriptedFrameDriver(config, new FrameScript { Palette = true });
            driver.PlaceSource!.AdditionalPlaces.AddRange(PaletteFixture.QuestPlaces);
            driver.Step(new(0));
            driver.Step(new(.02));
            driver.Palette!.Open("q:");
            driver.Step(new(.04));
            driver.Step(new(.06));
            var chips = driver.Palette.QuestChipBounds;
            Require(chips.Any(b => b != Vector4.Zero)
                && chips.Where(b => b != Vector4.Zero).All(b => b.X >= driver.Palette.PanelMin.X && b.Y >= driver.Palette.PanelMin.Y
                && b.Z <= driver.Palette.PanelMax.X && b.W <= driver.Palette.PanelMax.Y),
                "Wrapped quest chips fit narrow windows at every chrome scale");
            for (var i = 0; i < chips.Count; i++)
                for (var j = i + 1; j < chips.Count; j++)
                    Require(chips[i].Z <= chips[j].X || chips[j].Z <= chips[i].X
                        || chips[i].W <= chips[j].Y || chips[j].W <= chips[i].Y, "Quest chips never overlap");
            if (chips[^1] == Vector4.Zero) {
                var first = chips.First(b => b != Vector4.Zero);
                var point = new Vector2((first.X + first.Z) / 2, (first.Y + first.W) / 2);
                driver.Step(new(.08, Mouse: point));
                driver.Step(new(.10, Mouse: point, Wheel: -5));
                driver.Step(new(.12, Mouse: point));
                var last = driver.Palette.QuestChipBounds[^1];
                Require(last.Z > last.X && last.W > last.Y, "All buckets remain reachable by scrolling");
            }
        }
        Console.WriteLine("Quest buckets: Journal sections including zero/missing, six exclusive filters, "
            + "browse/zone counts, multiple destinations, q: queries, story action, reset and narrow chips.");
    }
}
