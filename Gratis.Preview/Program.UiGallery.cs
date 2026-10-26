using System.Numerics;
using System.Text.Json;
using SkiaSharp;

namespace Gratis.Preview;

internal static partial class Program {
    private static bool TryGalleryScene(string[] args) {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || args[at + 1] is not ("ui-gallery" or "ui-gallery-classic"))
            return false;
        var scene = args[at + 1];
        var outputAt = Array.IndexOf(args, "--out");
        var output = Path.GetFullPath(outputAt < 0 ? Path.Combine(Options.ProjectRoot(), "_preview", scene + ".png")
            : args[outputAt + 1]);
        var scratch = Path.Combine(Options.ProjectRoot(), "_artifacts", scene, "samples");
        var previousCaptures = visualCaptures;
        visualCaptures ??= new(StringComparer.Ordinal);
        try {
            var classic = scene.EndsWith("classic", StringComparison.Ordinal);
            var rows = CaptureGalleryControls(classic, scratch).ToList();
            rows.AddRange(CaptureGalleryPresenters(classic, scratch));
            DrawGallerySheet(rows, output, classic);
            File.WriteAllText(Path.ChangeExtension(output, ".coverage.json"), JsonSerializer.Serialize(rows,
                ScriptedFrameDriver.Json));
            Console.WriteLine($"{output}: {rows.Count} labelled rows; real controls and presenter crops.");
        }
        finally { visualCaptures = previousCaptures; }
        return true;
    }

    private static IEnumerable<GalleryRow> CaptureGalleryPresenters(bool classic, string scratch) {
        GalleryCell Source(string name, string region, string label, Vector4 fallback = default) {
            var spec = VisualCases().First(item => item.Name == name);
            var capture = RenderVisualCase(spec, scratch);
            var bounds = capture.Regions.GetValueOrDefault(region);
            if (bounds.Z <= bounds.X || bounds.W <= bounds.Y) bounds = fallback;
            if (bounds.Z <= bounds.X || bounds.W <= bounds.Y)
                throw new InvalidOperationException($"Gallery crop missing: {name}/{region}.");
            return new(label, capture.Path, bounds);
        }
        GalleryCell State(string name, string region, string state, string? target = null) {
            var spec = VisualCases().First(item => item.Name == name);
            var extra = new List<string> { "--state", state };
            if (target is not null) { extra.Add("--target"); extra.Add(target); }
            if (target == "pin" && state == "selected") extra.Add("--pinned");
            var capture = RenderVisualCase(spec with { Name = name + "-gallery-" + state + "-" + target,
                Args = spec.Args.Concat(extra).ToArray() }, scratch);
            var bounds = capture.Regions.GetValueOrDefault(region);
            if (bounds.Z <= bounds.X || bounds.W <= bounds.Y)
                throw new InvalidOperationException($"Gallery state crop missing: {name}/{region}/{state}.");
            return new(state, capture.Path, bounds);
        }
        var suffix = classic ? "-classic" : "";
        yield return new("Toolbar segments / divider", "MapToolbarDrawing (actual window)", [
            Source("library-toolbar" + suffix, "toolbar", "rest", new(32, 32, 800, 100)),
            Source("library-toolbar-hover" + suffix, "toolbar", "hover", new(32, 32, 800, 100)),
            State("library-toolbar" + suffix, "toolbar", "pressed"),
        ], 145);
        yield return new("Hover card / tooltip", "DrawHelpers.Interaction + HoverCardDrawing", [
            Source("hover-tooltip" + suffix, "hover", classic ? "native tooltip" : "marker hover card",
                new(300, 240, 850, 530)),
        ], 250);
        yield return new("Bottom cards / coordinates", "BottomCardsDrawing / MapCoordinateDrawing", [
            Source("library-bottom-cards" + suffix, "bottom", "real window", new(32, 560, 928, 708)),
        ], 190);
        yield return new("Settings rows / sections", "SettingsLayout (selected Chrome & motion page)", [
            Source("settings-chrome" + suffix, "settings-content", "rows, headers, dividers", new(110, 45, 890, 750)),
            Source("settings-search" + suffix, "settings-content", "search active", new(110, 45, 890, 750)),
        ], 470);
        yield return new("Settings navigation", "SettingsLayout.Gratis / Classic", [
            Source("settings-map" + suffix, "settings-nav", "Map selected", new(110, 45, 890, 140)),
            Source("tweaks-page" + suffix, "settings-content", "Tweaks rows", new(110, 45, 890, 750)),
        ], 360);
        yield return new("Move affordance", "MoveHandleInput (actual window)", [
            Source("library-move" + suffix, "move", "hover"),
        ], 120);
        if (classic) {
            yield return new("Domain-only presenters", "Production style gates are preserved", [new(
                "Palette, modern quests, filters, travel, pins, feedback and update notice are Gratis only.")], 100);
            yield break;
        }
        yield return new("Teleport action / card", "MapAssistance.TeleportCard (cropped presenter)", [
            Source("library-teleport", "travel", "rest"), State("library-teleport", "travel", "hover"),
            State("library-teleport", "travel", "pressed"), State("library-teleport", "travel", "focused"),
            Source("library-teleport-disabled", "travel", "disabled: confirmation guard"),
        ], 220);
        yield return new("Ghost notice / material", "MapWindowFrame.Ghost + real glass fixtures", [
            Source("library-ghost", "ghost", "ghost hint", new(180, 32, 780, 180)),
            Source("glass-card", "glass", "glass hover card", new(300, 240, 850, 530)),
        ], 220);
        yield return new("Update action / badge", "UpdateNotice (cropped presenter)", [
            Source("library-update", "update", "rest"), State("library-update", "update", "hover"),
            State("library-update", "update", "pressed"), State("library-update", "update", "focused"),
        ], 135);
        yield return new("Filter chip states", "FilterBarDrawing (real drawer)", [
            Source("library-filters", "filter", "normal"), Source("library-filters-quiet", "filter", "quiet"),
            Source("library-filters-hidden", "filter", "hidden"), Source("library-filters-solo", "filter", "solo"),
            Source("library-filters-loud", "filter", "loud"),
        ], 300);
        yield return new("Filter chip interaction", "FilterBarDrawing (Services chip)", [
            State("library-filters", "filter", "hover"), State("library-filters", "filter", "pressed"),
            State("library-filters", "filter", "focused"),
        ], 220);
        yield return new("Palette scope / quest segments", "PaletteOverlay (cropped presenter)", [
            Source("palette-home", "palette", "browse / scopes"), Source("palette-quests", "palette", "quest filters"),
        ], 410);
        yield return new("Palette scope states", "PaletteOverlay / ChromeControls.TextButton", [
            Source("palette-home", "button:##scope-1", "rest"),
            State("palette-home", "button:##scope-1", "hover", "scope"),
            State("palette-home", "button:##scope-1", "pressed", "scope"),
            State("palette-home", "button:##scope-1", "focused", "scope"),
            Source("palette-home", "button:##scope-0", "selected"),
        ], 120);
        yield return new("Palette quest-filter states", "PaletteOverlay / ChromeControls.TextButton", [
            Source("palette-quests", "button:##quest-bucket-1", "rest"),
            State("palette-quests", "button:##quest-bucket-1", "hover", "quest-filter"),
            State("palette-quests", "button:##quest-bucket-1", "pressed", "quest-filter"),
            State("palette-quests", "button:##quest-bucket-1", "focused", "quest-filter"),
            Source("palette-quests", "button:##quest-bucket-0", "selected"),
        ], 120);
        yield return new("Palette rows / artwork", "PaletteOverlay.Drawing + Browser", [
            Source("palette-home", "palette", "illustrated expansion cards"),
            Source("palette-query-limsa", "palette", "results / selected row"),
        ], 410);
        yield return new("Palette row interaction", "PaletteOverlay (real invisible target)", [
            State("palette-query-limsa", "palette", "hover", "row"),
            State("palette-query-limsa", "palette", "pressed", "row"),
        ], 300);
        yield return new("Search fields / empty states", "PaletteOverlay + QuestLogPanel", [
            Source("palette-empty", "palette", "no results"), Source("quest-log-available-empty", "quest-panel", "available empty"),
        ], 330);
        yield return new("Quest tabs", "QuestLogPanel (cropped presenter)", [
            Source("quest-log", "quest-tabs", "Accepted"), Source("quest-log-completed", "quest-tabs", "Completed"),
            Source("quest-log-available", "quest-tabs", "Available"),
        ], 125);
        yield return new("Quest tab interaction", "QuestLogPanel (Completed tab)", [
            State("quest-log", "quest-tabs", "hover", "tab"),
            State("quest-log", "quest-tabs", "pressed", "tab"),
            State("quest-log", "quest-tabs", "focused", "tab"),
        ], 125);
        yield return new("Quest pin states", "QuestLogPanel (real pin)", [
            Source("quest-log", "quest-pin", "rest"), State("quest-log", "quest-pin", "hover", "pin"),
            State("quest-log", "quest-pin", "pressed", "pin"), State("quest-log", "quest-pin", "focused", "pin"),
            State("quest-log", "quest-pin", "selected", "pin"),
        ], 120);
        yield return new("Quest row interaction", "QuestLogPanel (first real row)", [
            Source("quest-log", "quest-row", "rest"), State("quest-log", "quest-row", "hover", "row"),
            State("quest-log", "quest-row", "pressed", "row"), State("quest-log", "quest-row", "focused", "row"),
            State("quest-log", "quest-row", "selected", "row"),
        ], 160);
        yield return new("Quest rows / sections / scrollbar", "QuestLogPanel (real fixture)", [
            Source("quest-log", "quest-list", "Accepted rows / sticky headers"),
            Source("quest-log-search", "quest-panel", "search open"),
        ], 410);
        yield return new("Quest map action states", "QuestLogPanel.Summary (real map action)", [
            Source("quest-log-detail", "quest-map", "rest"), State("quest-log-detail", "quest-map", "hover"),
            State("quest-log-detail", "quest-map", "pressed"), State("quest-log-detail", "quest-map", "focused"),
            Source("quest-log-detail-completed", "quest-map", "disabled: no destination"),
        ], 130);
        yield return new("Quest progress", "QuestLogPanel.Progress", [Source("quest-log", "quest-progress", "127 / 143")], 120);
        yield return new("Quest map / rewards / badges", "QuestLogPanel.Summary (cropped presenter)", [
            Source("quest-log-detail", "quest-detail", "map action, reward slots, quantities and metrics"),
        ], 410);
        yield return new("Item content tooltip / menu", "ItemTooltipDrawing (actual hovered reward)", [
            Source("quest-log-detail-tooltip", "item-tooltip", "tooltip", new(350, 80, 1080, 650)),
            Source("quest-log-detail-menu", "item-menu", "right-click menu", new(350, 80, 1080, 650)),
        ], 380);
        yield return new("Location menu / search", "LocationMenuDrawing (actual opened dropdown)", [
            Source("library-location", "popup", "open"), Source("library-location-exit", "popup", "40 ms into exit"),
        ], 400);
        yield return new("Context menu / popup lifecycle", "ContextMenu + UiMotion.Popup", [
            Source("library-popup", "popup", "rest"), Source("library-popup-exit", "popup", "40 ms into exit"),
        ], 370);
        yield return new("Feedback form", "FeedbackButtonDrawing (actual popup)", [
            Source("library-feedback", "popup", "rest / unavailable Send"),
            Source("library-feedback-exit", "popup", "40 ms into exit"),
        ], 365);
        yield return new("Pin editor / colour swatches", "PinOverlay (cropped presenter)", [
            Source("pin-editor", "pin-editor", "title / note / colours / buttons"),
        ], 370);
    }

    private static void DrawGallerySheet(List<GalleryRow> rows, string output, bool classic) {
        const int width = 1800, labelWidth = 310, header = 128;
        using var bitmap = new SKBitmap(width, header + rows.Sum(row => row.Height) + 48);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(18, 20, 25));
        using var typeface = SKTypeface.FromFile(Path.Combine(AppContext.BaseDirectory, "assets", "NotoSans-Regular.ttf"));
        using var font = new SKFont(typeface, 14);
        using var small = new SKFont(typeface, 11);
        using var title = new SKFont(typeface, 25);
        using var ink = new SKPaint { Color = new SKColor(231, 234, 240), IsAntialias = true };
        using var muted = new SKPaint { Color = new SKColor(147, 157, 174), IsAntialias = true };
        using var line = new SKPaint { Color = new SKColor(48, 52, 63) };
        canvas.DrawText("Gratis UI component gallery" + (classic ? " / Classic" : " / Gratis"), 24, 38, title, ink);
        canvas.DrawText("Real drawing code. Native mouse / keyboard frames; domain presenters are cropped, never redrawn.",
            24, 65, font, muted);
        canvas.DrawText("State columns apply to primitives. Presenter rows label their own states. No such state = no supplied state contract.",
            24, 89, font, muted);
        for (var i = 0; i < GalleryStates.Length; i++)
            canvas.DrawText(GalleryStates[i], labelWidth + i * (width - labelWidth) / 6 + 12, 118, font, ink);
        var y = header;
        foreach (var row in rows) {
            canvas.DrawLine(16, y, width - 16, y, line);
            canvas.DrawText(row.Label, 20, y + 27, font, ink);
            var source = row.Source.Length > 40 ? row.Source[..37] + "..." : row.Source;
            canvas.DrawText(source, 20, y + 47, small, muted);
            var cellWidth = (width - labelWidth) / (float)row.Cells.Length;
            for (var i = 0; i < row.Cells.Length; i++) {
                var cell = row.Cells[i];
                var x = labelWidth + i * cellWidth + 10;
                if (cell.Path is null) {
                    canvas.DrawText(cell.Label, x, y + 45, small, muted);
                    continue;
                }
                canvas.DrawText(cell.Label, x, y + 19, small, muted);
                using var sourceImage = SKBitmap.Decode(cell.Path) ?? throw new InvalidDataException(cell.Path);
                var b = cell.Bounds;
                var rect = SKRect.Create(Math.Clamp(b.X - 4, 0, sourceImage.Width), Math.Clamp(b.Y - 4, 0, sourceImage.Height),
                    Math.Min(b.Z + 4, sourceImage.Width) - Math.Max(0, b.X - 4),
                    Math.Min(b.W + 4, sourceImage.Height) - Math.Max(0, b.Y - 4));
                if (rect.Width <= 0 || rect.Height <= 0) throw new InvalidOperationException("Invalid gallery crop.");
                var scale = Math.Min(1, Math.Min((cellWidth - 20) / rect.Width, (row.Height - 34) / rect.Height));
                canvas.DrawBitmap(sourceImage, rect, SKRect.Create(x, y + 27, rect.Width * scale, rect.Height * scale));
            }
            y += row.Height;
        }
        canvas.DrawText("Extraction gaps and fixture fidelity: Gratis.Preview/ui-gallery-notes.md", 24, y + 28, font, muted);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        SaveGalleryBitmap(bitmap, output);
    }

    private static void SaveGalleryBitmap(SKBitmap bitmap, string path) {
        using var stream = File.Create(path);
        if (!bitmap.Encode(stream, SKEncodedImageFormat.Png, 100)) throw new IOException("Cannot encode gallery PNG.");
    }
}
