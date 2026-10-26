using System.Globalization;
using System.Numerics;
using System.Reflection;
using Gratis.Controllers;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;

namespace Gratis.Preview;

internal static class PerformanceCacheVerification {
    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Zero(Action action, string name) {
        for (var i = 0; i < 64; i++) action();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1024; i++) action();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(bytes == 0, $"{name} allocated {bytes} bytes after warm-up.");
    }

    public static void Run() {
        var original = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Quests();
            Items();
            Coordinates();
            Palette();
            Settings();
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    private static void Quests() {
        var first = new QuestLogQuest(1, "First", 5, QuestBucket.Scenario, 0, "", "", []);
        var current = first with { RowId = 2, Name = "Current", Level = 10 };
        var locked = first with { RowId = 3, Name = "Locked", Repeatable = true };
        var snapshot = new QuestLogSnapshot(1, QuestLogStatus.Ready, [first, current], current.Name) {
            Completed = [first with { Completed = true }], Unaccepted = [locked],
        };
        var cache = new QuestLogPresentation();
        cache.Update(snapshot);
        cache.Filter("", QuestLogTab.Accepted, false, false);
        Require(cache.Groups[0].Quests.SequenceEqual(new[] { current, first }), "Current MSQ ordering changed.");
        Require(ReferenceEquals(cache.Find(1), first), "Summary lookup lost accepted-first precedence.");
        Require(cache.For(current).Id == "2" && cache.For(current).Level == "Lv. 10", "Row strings changed.");
        Zero(() => {
            cache.Update(snapshot);
            cache.Filter("", QuestLogTab.Accepted, false, false);
            _ = cache.Find(1);
            _ = cache.For(current);
        }, "Unchanged quest presentation");
        cache.Filter("", QuestLogTab.Unaccepted, false, false);
        Require(cache.Filtered.Length == 0, "Locked quests leaked.");
        cache.Filter("", QuestLogTab.Unaccepted, true, true);
        Require(cache.Filtered.Single() == locked, "Spoiler/repeatable invalidation failed.");
        cache.Filter("missing", QuestLogTab.Unaccepted, true, true);
        Require(cache.Groups.Length == 0, "Search invalidation failed.");
        var changed = current with { Level = 20 };
        cache.Update(snapshot with { Revision = 2, Quests = [changed], CurrentScenario = "" });
        cache.Filter("", QuestLogTab.Accepted, false, false);
        Require(cache.ScenarioId == 0 && cache.Find(1)?.Completed == true && cache.For(changed).Level == "Lv. 20",
            "Snapshot replacement retained stale presentation.");
        cache.Update(snapshot with { Quests = [changed] });
        Require(ReferenceEquals(cache.Find(2), changed), "A new snapshot with the same revision was ignored.");
    }

    private static void Items() {
        var item = new ItemTooltipRecord(1, "Item", 0) { ShopPrice = 12345, StackSize = 99 };
        var sections = ItemTooltipModel.Sections(item);
        Require(sections.Single().Text == "Shop selling price: 12,345 gil", "Item formatting changed.");
        Zero(() => {
            var text = ItemTooltipModel.For(item);
            _ = text.Sections;
            _ = text.Stack(item, 2, 3);
        }, "Unchanged item presentation");
        Require(ItemTooltipModel.For(item).Stack(item, 2, 4) == ItemTooltipModel.Stack(item, 2, 4),
            "Inventory count retained stale text.");
        Require(ItemTooltipModel.Sections(item with { Effects = "New effect" }).Count == 2,
            "Replacement item retained stale sections.");
        var mutable = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        CultureInfo.CurrentCulture = mutable;
        mutable.NumberFormat.NumberGroupSeparator = "_";
        Require(ItemTooltipModel.Sections(item).Single().Text.Contains("12_345"), "Culture invalidation failed.");
        mutable.NumberFormat.NumberGroupSeparator = ":";
        Require(ItemTooltipModel.Sections(item).Single().Text.Contains("12:345"), "Mutable culture retained stale text.");
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    private static void Coordinates() {
        var cache = new CoordinateTextCache();
        foreach (var point in new[] { new Vector2(1.25f, -0f), new(-0f, 999.95f), new(float.NaN, float.PositiveInfinity) }) {
            var expected = point.X.ToString("0.0", CultureInfo.InvariantCulture).PadLeft(4) + " / "
                + point.Y.ToString("0.0", CultureInfo.InvariantCulture).PadRight(4);
            Require(cache.Format(point) == expected, "Coordinate formatting changed.");
        }
        Require(cache.Format(null) == "--.- / --.-", "Missing coordinates changed.");
        var value = new Vector2(10, 20);
        Zero(() => { _ = cache.Format(value); }, "Unchanged coordinates");
        Zero(() => { _ = EorzeaTime.Format(100); }, "Unchanged Eorzea minute");
        Require(EorzeaTime.Format(0) == "ET 00:00" && EorzeaTime.Format(-1) == "ET 23:59", "Clock invalidation failed.");
    }

    private static void Palette() {
        var place = new PlaceEntry("one", PlaceKind.Marker, "Aetheryte", "Zone > Area", 1, 1, new(100, 20));
        PlaceResult[] results = [new(place, 1, default, 100), new(place with { Id = "two" }, 0, default, 100)];
        var cache = new PaletteRowNames();
        foreach (var result in results)
            Require(cache.Get(result, results) == PaletteBrowser.Name(result, results), "Duplicate naming changed.");
        Zero(() => { _ = cache.Get(results[0], results); }, "Unchanged palette name");
        PlaceResult[] next = [results[0] with { Distance = 200 }];
        Require(cache.Get(next[0], next) == PaletteBrowser.Name(next[0], next), "Result replacement retained stale names.");
        Require(cache.Get(results[0], results) == PaletteBrowser.Name(results[0], results), "Outgoing page names changed.");
    }

    private static void Settings() {
        var layout = new SettingsLayout();
        var config = new SystemConfig();
        var prepare = typeof(SettingsLayout).GetMethod("VisibleCatalogue", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var style in new[] { 0, 1, 0 }) {
            config.VisualStyle = style;
            config.AutoHideChrome = style != 0;
            foreach (var search in new[] { "", "title", "chrome", "", "no such setting" }) {
                layout.Search = search;
                foreach (var section in SettingsLayout.Sections) {
                    var catalogue = prepare.Invoke(layout, [config, section])!;
                    var groups = (global::System.Collections.IEnumerable)catalogue.GetType().GetField("Visible")!.GetValue(catalogue)!;
                    var expected = SettingsLayout.Find(config, section, search).GroupBy(e => e.Group).ToArray();
                    var index = 0;
                    foreach (var group in groups) {
                        var type = group.GetType();
                        var visible = (IEnumerable<SettingsEntry>)type.GetField("Visible")!.GetValue(group)!;
                        Require(visible.SequenceEqual(expected[index++]), "Settings visibility/order cache changed results.");
                        type.GetMethod("PrepareRows")!.Invoke(group, [config, !string.IsNullOrWhiteSpace(search)]);
                        var rows = (IEnumerable<SettingsEntry>)type.GetField("Rows")!.GetValue(group)!;
                        Require(rows.SequenceEqual(visible.Where(e => e.Enabled(config) || !string.IsNullOrWhiteSpace(search))),
                            "Settings enabled-state cache changed results.");
                    }
                    Require(index == expected.Length, "Settings cache lost a group.");
                }
            }
        }
    }
}
