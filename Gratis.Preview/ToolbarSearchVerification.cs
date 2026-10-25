using System.Numerics;
using System.Text.Json;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;
using ImGuiNET;

namespace Gratis.Preview;

internal static class ToolbarSearchVerification
{
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    internal static Vector2 Center(Vector4 b) => new((b.X + b.Z) / 2, (b.Y + b.W) / 2);
    private static bool Valid(Vector4 b) => b.Z > b.X && b.W > b.Y;
    internal static void Click(ScriptedFrameDriver driver, Vector4 bounds, ref double time)
    {
        var point = Center(bounds);
        driver.Step(new(time += .04, Mouse: point, Left: false));
        driver.Step(new(time += .04, Left: true));
        driver.Step(new(time += .04, Left: false));
        driver.Step(new(time += .04));
        driver.Step(new(time += .04));
    }

    public static void Run()
    {
        var old = JsonSerializer.Deserialize<SystemConfig>("{\"PaletteShortcut\":\"Ctrl+K\"}", ScriptedFrameDriver.Json)!;
        Require(old.Palette && SettingsLayout.Entries.All(e => e.Path != "PaletteShortcut"), "Old shortcut configs load without exposing a binding");
        foreach (var width in new[] { 260, 700, 1100 }) {
            var c = PaletteVerification.Config(); c.PinChrome = true; c.WindowSize = new(width, 620);
            using var driver = new ScriptedFrameDriver(c, new FrameScript { Palette = true, Width = 1280, Height = 800 });
            double time = 0;
            driver.Step(new(time)); driver.Step(new(time += .04));
            var boxes = MapToolbarDrawing.ButtonBounds.ToArray();
            var centers = boxes.Where(Valid).Select(Center).ToArray();
            Require(centers.All(p => MathF.Abs(p.Y - centers[0].Y) < .001f), "Toolbar hit boxes have equal centre lines");
            for (var i = 0; i < boxes.Length; i++) {
                if (!Valid(boxes[i])) continue;
                var glyph = MapToolbarDrawing.GlyphBounds[i];
                Require(Valid(glyph) && Vector2.Distance(Center(glyph), Center(boxes[i])) < .001f, "Measured glyph ink shares the button centre, including Close");
                Require(MathF.Abs(boxes[i].Z - boxes[i].X - GratisTheme.ControlHeight) < .001f &&
                    MathF.Abs(boxes[i].W - boxes[i].Y - GratisTheme.ControlHeight) < .001f, "Every icon retains its equal hit target");
            }
            var dividers = MapToolbarDrawing.DividerBounds.Where(Valid).ToArray();
            Require(dividers.Length == (width == 260 ? 2 : 4), "Functional groups have thin vertical dividers");
            Require(dividers.All(d => boxes.All(b => !ChromeLayout.Overlap(b, d))), "Dividers stay outside all button hit rectangles");
            var hover = Center(boxes[0]);
            var previousTooltipTime = ChromeControls.LastTooltipTime;
            driver.Step(new(time += .04, Mouse: hover));
            var started = time;
            driver.Step(new(time = started + .38));
            Require(ChromeControls.LastTooltipTime == previousTooltipTime, "Tooltip waits 400 ms");
            driver.Step(new(time = started + .44));
            var tooltip = ChromeControls.LastTooltipBounds;
            Require(ChromeControls.LastTooltipAnchor == boxes[0] && ChromeControls.LastTooltipTime >= started + .4, "Tooltip belongs to hovered toolbar button");
            Require(MathF.Abs(tooltip.Y - boxes[0].W - 4) < .01f, "Tooltip sits directly below its button");
            Require(tooltip.X >= c.WindowPosition.X && tooltip.Z <= c.WindowPosition.X + width, "Tooltip clamps to map window");
            Require(MapToolbarDrawing.ButtonBounds.SequenceEqual(boxes), "Hover and divider drawing do not shift hit rectangles");
            driver.Step(new(time += .04, Control: true, Keys: [ImGuiKey.K]));
            Require(!driver.Palette!.IsOpen, "Retired shortcut cannot open search");
            driver.Step(new(time += .04, Control: false));
            Click(driver, MapToolbarDrawing.ButtonBounds[0], ref time);
            var palette = driver.Palette;
            Require(palette.IsOpen && Valid(palette.FieldBounds) && palette.Query == "", "Toolbar click expands the inline field and home");
            var toolbar = MapToolbarDrawing.LastBounds;
            Require(palette.FieldBounds.X >= toolbar.X && palette.FieldBounds.Z <= toolbar.Z, "Field stays inside toolbar at narrow widths");
            Require(palette.PanelMin.Y >= palette.FieldBounds.W && palette.PanelMax.X <= c.WindowPosition.X + width, "Home dropdown anchors below the field and clamps");
            driver.Step(new(time += .04, Text: "limsa"));
            Require(palette.Query == "limsa" && palette.Results.Count > 1, $"Inline input autofocus accepts native text and ranks results: width={width}, query={palette.Query}, results={palette.Results.Count}, open={palette.IsOpen}, capture={palette.CapturesInput}");
            driver.Step(new(time += .04, Keys: [ImGuiKey.DownArrow]));
            var selected = palette.Results[palette.Selected].Place;
            Require(palette.Selected == 1, "Inline results accept arrow navigation");
            driver.Step(new(time += .04)); driver.Step(new(time += .04, Keys: [ImGuiKey.Enter]));
            Require(!palette.IsOpen && driver.PlaceSource!.Shown == selected, "Enter selects and collapses inline search");
            driver.Step(new(time += .04));
            Click(driver, MapToolbarDrawing.ButtonBounds[0], ref time);
            driver.Step(new(time += .04, Text: "limsa"));
            Click(driver, palette.ClearBounds, ref time);
            Require(palette.Query == "" && palette.IsOpen, $"Clear restores palette home without closing: width={width}, open={palette.IsOpen}, query={palette.Query}");
            var point = c.WindowPosition + new Vector2(5, c.WindowSize.Y / 2);
            driver.Step(new(time += .04, Mouse: point, Left: true));
            Require(!palette.IsOpen && !palette.CapturesInput, "Outside mouse-down dismisses search before map input");
            var before = driver.Renderer.DrawOffset;
            var dragged = driver.Step(new(time += .04, Mouse: point + new Vector2(35, 18)));
            Require(dragged.Dragging && driver.Renderer.DrawOffset != before, "The dismissing map gesture continues as a drag");
            driver.Step(new(time += .04, Left: false));
            Click(driver, MapToolbarDrawing.ButtonBounds[0], ref time);
            driver.Step(new(time += .04, Escape: true));
            Require(!palette.IsOpen, "Esc collapses inline search");
            c.ToolbarAnchor = ChromeAnchor.BottomCenter;
            driver.Step(new(time += .04, Escape: false, Mouse: new(-1000)));
            var bottomButton = MapToolbarDrawing.ButtonBounds[0];
            driver.Step(new(time += .04, Mouse: Center(bottomButton)));
            driver.Step(new(time += .44));
            Require(ChromeControls.LastTooltipAnchor == bottomButton &&
                MathF.Abs(ChromeControls.LastTooltipBounds.W - bottomButton.Y + 4) < .01f,
                "Actual toolbar tooltip flips above a bottom anchor at each window width");
        }
        var flipped = ChromePopoverMath.Place(new(85, 180, 121, 216), new(160, 36), Vector2.Zero, new(240), 4);
        Require(flipped.W == 176 && flipped.X >= 0 && flipped.Z <= 240, "Labels flip immediately above when below has no room");
        var config = PaletteVerification.Config(); config.PinChrome = true;
        using (var driver = new ScriptedFrameDriver(config, new FrameScript { Palette = true })) {
            double time = 0; driver.Step(new(time)); driver.Step(new(time += .04));
            Click(driver, MapToolbarDrawing.ButtonBounds[2], ref time);
            Require(FilterBarDrawing.PopoverOpen, "Toolbar Filters opens the drawer");
            var button = MapToolbarDrawing.ButtonBounds[2]; var drawer = FilterBarDrawing.PopoverBounds;
            Require(MathF.Abs(drawer.Y - button.W - GratisTheme.Gap) < .01f, "Drawer anchors directly below toolbar Filters");
            Click(driver, MapToolbarDrawing.ButtonBounds[2], ref time);
            Require(!FilterBarDrawing.PopoverOpen, "Toolbar Filters toggles closed without reopening");
            Click(driver, MapToolbarDrawing.ButtonBounds[2], ref time);
            driver.Step(new(time += .04, Escape: true)); driver.Step(new(time += .04, Escape: false));
            Require(!FilterBarDrawing.PopoverOpen, "Esc closes toolbar drawer");
            Click(driver, MapToolbarDrawing.ButtonBounds[2], ref time);
            driver.Step(new(time += .04, Mouse: config.WindowPosition + new Vector2(5, 300), Left: true));
            driver.Step(new(time += .04, Left: false));
            Require(!FilterBarDrawing.PopoverOpen, "Outside click closes toolbar drawer");
        }
        Console.WriteLine("Toolbar: measured glyph/hit centres, divider gaps, 400ms anchored labels at three widths, native inline typing/clear/navigation/selection, outside drag/Esc dismissal and toolbar drawer toggling.");
    }
}
