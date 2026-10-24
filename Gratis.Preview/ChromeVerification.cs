using System.Numerics;
using Gratis.Classes;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class ChromeVerification
{
    public static void Run(PreviewContext context, Textures textures)
    {
        VerifyTimeline();
        var config = new MapDrawingConfig();
        var state = new ChromeVisibility();
        state.Reveal(10);
        state.Update(config, 11.2, false);
        Near(state.Progress, 1, "Idle delay");
        state.Update(config, 11.245, false);
        Near(state.Progress, .84375f, "Eased quarter fade");
        state.Update(config, 11.29, false);
        Near(state.ControlsAlpha(config), .5f, "Mid-fade controls");
        Near(state.TitleAlpha(config), .675f, "Mid-fade title");
        state.Update(config, 12, false);
        Near(state.ControlsAlpha(config), 0, "Hidden controls");
        Near(state.TitleAlpha(config), .35f, "Hidden title");
        state.Update(config, 13, true);
        Near(state.Progress, 1, "Immediate interaction reveal");
        config.PinChrome = true;
        state.Update(config, 30, false);
        Near(state.ControlsAlpha(config), 1, "Pin");
        config.PinChrome = false; config.AutoHideChrome = false;
        state.Update(config, 60, false);
        Near(state.ControlsAlpha(config), 1, "Auto-hide disabled");
        config.AutoHideChrome = true; config.VisualStyle = 1;
        state.Update(config, 90, false);
        Near(state.ControlsAlpha(config), 1, "Classic bypass");
        config.VisualStyle = 0; config.ChromeFadeDurationMs = 0;
        state.Reveal(100); state.Update(config, 102, false);
        Near(state.Progress, 0, "Zero-duration fade");
        config.ChromeIdleDelay = float.NaN; config.ChromeFadeDurationMs = float.NaN;
        config.ChromeTitleMinAlpha = float.NaN; config.ChromeControlsMinAlpha = float.NaN;
        state.Reveal(110); state.Update(config, 112, false);
        Near(state.TitleAlpha(config), .35f, "Invalid saved values");
        config.AutoHideChrome = false; config.PinChrome = true;
        config.ResetChromeVisibility();
        Require(config.AutoHideChrome && !config.PinChrome && config.ChromeIdleDelay == 1.2f &&
            config.ChromeFadeDurationMs == 180 && config.ChromeTitleMinAlpha == .35f && config.ChromeControlsMinAlpha == 0,
            "Chrome reset");

        context.Config.VisualStyle = 0;
        ImGui.NewFrame();
        ImGui.Begin("Lucide glyph verification");
        foreach (var size in new[] { 12, 16, 24 }) {
            var handle = context.GetIconFont(size);
            Require(handle is not null, "Lucide font missing");
            using var fontScope = handle!.Push();
            foreach (var codepoint in GratisIcons.GlyphRanges.Where((_, i) => i % 2 == 0 && i < GratisIcons.GlyphRanges.Length - 1)) {
                Require(DrawBinding.TryGlyphBounds(ImGui.GetFont(), (char)codepoint, out var bounds) &&
                    bounds.Z > bounds.X && bounds.W > bounds.Y, $"Lucide glyph U+{codepoint:X4} at {size}px");
            }
        }
        ImGui.End();
        ImGui.Render();
        VerifyInputAndAlpha(context);
        VerifyFrameHover(context);
        VerifyPixels(context, textures);
        Console.WriteLine("Chrome selftest passed: timeline/settings/clock/counters, minima/pin/Classic/reset, Lucide glyphs, hidden input pass-through, one-frame hover reveal, toolbar pixels and parent alpha.");
    }

    private static void VerifyTimeline()
    {
        var config = new MapDrawingConfig();
        var state = new ChromeVisibility();
        state.Update(config, 0, ChromeActivity.None);
        state.Update(config, 5, ChromeActivity.None);
        Near(state.Progress, 0, "Idle five seconds hides");
        state.Update(config, 5 + 1d / 60, ChromeActivity.Hover);
        Near(state.Progress, 1, "Hover reveals in one frame");
        var leave = 5 + 1d / 60;
        state.Update(config, leave + config.ChromeIdleDelay, ChromeActivity.None);
        Near(state.Progress, 1, "Leave retains idle delay");
        state.Update(config, leave + config.ChromeIdleDelay + config.ChromeFadeDurationMs / 2000d, ChromeActivity.None);
        Near(state.Progress, .5f, "Leave half fade");
        state.Update(config, leave + config.ChromeIdleDelay + config.ChromeFadeDurationMs / 1000d + .001, ChromeActivity.None);
        Near(state.Progress, 0, "Leave hides after delay plus fade");
        for (var frame = 0; frame < 300; frame++) state.Update(config, 8 + frame / 60d, ChromeActivity.Settings);
        Near(state.Progress, 0, "Settings open for five seconds never reveals");
        Require(state.SecondsSinceActivity(13) > 7.9, "Settings did not reset last activity");
        var diagnostics = state.TakeDiagnostics(config, 13);
        Require(diagnostics.Contains("hover=1") && diagnostics.Contains("settings=0") && diagnostics.Contains("settings-open=300"), "Reason counters");
        Require(state.TakeDiagnostics(config, 13).Contains("hover=0"), "Diagnostic counts reset");
        Near(state.Progress, 0, "Diagnostic read does not reveal");
        state.Update(config, double.NaN, ChromeActivity.None);
        state.Update(config, 14, ChromeActivity.None);
        Near(state.Progress, 0, "Invalid time does not poison state");
        state.Update(config, 1, ChromeActivity.None);
        Near(state.Progress, 1, "Clock rewind initializes a fresh idle interval");
        state.Update(config, 6, ChromeActivity.None);
        Near(state.Progress, 0, "Clock rewind does not keep revealing");
        state.Update(config, 7, ChromeActivity.Drag | ChromeActivity.FocusedActiveItem);
        Near(state.Progress, 1, "Map drag/active item reveals");
        state.Reveal(8, "MapContextMenu.cs", "Draw");
        state.Reveal(9, "HoverCardDrawing.cs", "Draw");
        diagnostics = state.TakeDiagnostics(config, 9);
        Require(diagnostics.Contains("drag=1") && diagnostics.Contains("focused-active-item=1") &&
            diagnostics.Contains("popup=1") && diagnostics.Contains("hovercard=1") &&
            diagnostics.Contains("external:MapContextMenu.Draw=1") && diagnostics.Contains("clock-reset=1"), "Named reveal counters");
    }

    private static void VerifyInputAndAlpha(PreviewContext context)
    {
        var c = context.Config;
        c.ResetChromeVisibility(); c.MapCircle = false; c.SegmentedToolbar = true;
        c.HideWindowFrame = true; c.GlassEnabled = true; c.GlassBlur = true;
        var io = ImGui.GetIO();
        var trickle = io.ConfigInputTrickleEventQueue;
        io.ConfigInputTrickleEventQueue = false;
        var activations = 0;
        try {
            for (var step = 0; step < 5; step++) {
                var mouse = step < 2 ? new Vector2(-1000) : new Vector2(54, 50);
                io.AddMousePosEvent(mouse.X, mouse.Y);
                io.AddMouseButtonEvent(0, step == 3);
                ImGui.NewFrame();
                ImGui.SetNextWindowPos(new Vector2(28, 24));
                ImGui.SetNextWindowSize(new Vector2(264, 192));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                ImGui.Begin("chrome input verification", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings);
                ImGui.PopStyleVar();
                MapShape.Update(ImGui.GetWindowSize());
                if (step == 0) context.Chrome.Reveal(ImGui.GetTime() - 2);
                context.Chrome.Update(c, ImGui.GetTime(), ChromeVisibility.IsMapFrameHovered());
                if (step == 1) {
                    context.Blurs.Clear();
                    var list = ImGui.GetWindowDrawList();
                    var start = list.VtxBuffer.Size;
                    using (new ChromeVisibility.AlphaScope(true, context.Chrome.ControlsAlpha(c)))
                        MapWindowDrawing.DrawSurface(list, new Vector2(40, 80), new Vector2(200, 110), 8);
                    Require(context.Blurs.Count == 0 && list.VtxBuffer.Size == start, "Hidden glass emits no surface or blur");
                    Near(context.Chrome.Progress, 0, "Input fixture starts hidden");
                }
                MapToolbarDrawing.Draw(action => { if (action == ToolbarAction.Follow) activations++; }, () => { });
                ImGui.End();
                ImGui.Render();
                if (step >= 2) Near(context.Chrome.Progress, 1, "Hover reveals before input");
            }
            Require(activations == 1, $"Revealed follow click dispatched {activations} times");
            var mapClicks = 0;
            var hiddenToolbarClicks = 0;
            for (var step = 0; step < 4; step++) {
                io.AddMousePosEvent(54, 50);
                io.AddMouseButtonEvent(0, step == 2);
                ImGui.NewFrame();
                ImGui.SetNextWindowPos(new Vector2(28, 24));
                ImGui.SetNextWindowSize(new Vector2(264, 192));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                ImGui.Begin("hidden toolbar pass-through", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings);
                ImGui.PopStyleVar();
                MapShape.Update(ImGui.GetWindowSize());
                context.Chrome.Update(c, ImGui.GetTime(), false, step % 2 == 0 ? 0 : .049f);
                ImGui.SetCursorPos(Vector2.Zero);
                if (ImGui.InvisibleButton("map click target", ImGui.GetWindowSize())) mapClicks++;
                ImGui.SetCursorPos(Vector2.Zero);
                MapToolbarDrawing.Draw(_ => hiddenToolbarClicks++, () => { });
                ImGui.End();
                ImGui.Render();
            }
            Require(mapClicks == 1 && hiddenToolbarClicks == 0, "Below 5% toolbar input passes to map");
        }
        finally { io.ConfigInputTrickleEventQueue = trickle; }
    }

    private static void VerifyPixels(PreviewContext context, Textures textures)
    {
        var io = ImGui.GetIO();
        var displaySize = io.DisplaySize;
        io.DisplaySize = new Vector2(360, 260);
        try {
            foreach (var segmented in new[] { true, false }) {
                context.Config.SegmentedToolbar = segmented;
                foreach (var glass in new[] { true, false }) {
                    context.Config.GlassEnabled = glass;
                    var energies = new List<float>();
                    foreach (var progress in new[] { 0f, .5f, 1f }) {
                        for (var frame = 0; frame < 2; frame++) {
                            context.Blurs.Clear();
                            io.AddMousePosEvent(-1000, -1000);
                            ImGui.NewFrame();
                            ImGui.SetNextWindowPos(new Vector2(28, 24));
                            ImGui.SetNextWindowSize(new Vector2(264, 192));
                            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                            ImGui.Begin("toolbar pixel verification", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings);
                            ImGui.PopStyleVar();
                            MapShape.Update(ImGui.GetWindowSize());
                            context.Chrome.Update(context.Config, ImGui.GetTime(), false, progress);
                            MapToolbarDrawing.Draw(_ => { }, () => { });
                            ImGui.End();
                            ImGui.Render();
                        }
                        var renderer = new SoftwareRenderer(360, 260, textures);
                        renderer.Render(ImGui.GetDrawData(), context.Blurs);
                        var background = renderer.Pixel(0, 0);
                        var energy = 0f;
                        var pixels = 0;
                        for (var y = 0; y < 260; y++)
                            for (var x = 0; x < 360; x++) {
                                var distance = Vector4.Distance(renderer.Pixel(x, y), background);
                                energy += distance;
                                if (distance > 0) pixels++;
                            }
                        if (progress == 0) Require(pixels == 0 && context.Blurs.Count == 0, "Hidden toolbar emits zero pixels/blur");
                        energies.Add(energy);
                    }
                    Require(energies[0] == 0 && energies[1] > 0 && energies[2] > energies[1], $"Toolbar fades visibly (segmented={segmented}, glass={glass}): {string.Join('/', energies)}");
                    Console.WriteLine($"Toolbar pixels: hidden=0; fade/visible energy={energies[1]:0.0}/{energies[2]:0.0}; segmented={segmented}, glass={glass}.");
                }
            }
            ImGui.NewFrame();
            ImGui.Begin("parent fade verification");
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, .4f);
            using (var scope = new ChromeVisibility.AlphaScope(true, .5f)) Near(ImGui.GetStyle().Alpha, .2f, "Chrome multiplies parent fade");
            Near(ImGui.GetStyle().Alpha, .4f, "Parent alpha restored");
            ImGui.PopStyleVar();
            ImGui.End();
            ImGui.Render();
        }
        finally { io.DisplaySize = displaySize; }
    }

    private static void VerifyFrameHover(PreviewContext context)
    {
        context.Config.MapCircle = true;
        var io = ImGui.GetIO();
        foreach (var (point, covered, expected) in new[] {
            (new Vector2(10, 10), false, false),
            (new Vector2(40, 120), false, false),
            (new Vector2(65, 25), false, false),
            (new Vector2(160, 120), false, true),
            (new Vector2(160, 120), true, false),
        }) {
            var hovered = false;
            var settingsActive = false;
            var mapActive = false;
            var text = "settings";
            for (var frame = 0; frame < 3; frame++) {
                io.AddMousePosEvent(point.X, point.Y);
                ImGui.NewFrame();
                if (covered) {
                    ImGui.SetNextWindowPos(new Vector2(100, 80));
                    ImGui.SetNextWindowSize(new Vector2(160, 100));
                    ImGui.SetNextWindowFocus();
                    ImGui.Begin("active settings verification", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings);
                    ImGui.SetKeyboardFocusHere();
                    ImGui.InputText("setting", ref text, 32);
                    settingsActive = ImGui.IsItemActive();
                    ImGui.End();
                }
                ImGui.SetNextWindowPos(new Vector2(28, 24));
                ImGui.SetNextWindowSize(new Vector2(264, 192));
                ImGui.Begin("map bounds verification", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground |
                    ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing);
                MapShape.Update(ImGui.GetWindowSize());
                hovered = ChromeVisibility.IsMapFrameHovered();
                mapActive = ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows) && ImGui.IsAnyItemActive();
                ImGui.End();
                ImGui.Render();
            }
            Require(hovered == expected, $"Screen-space map frame hover at {point}, covered={covered}");
            if (covered) Require(settingsActive && !mapActive, "An active settings item does not count as a map item");
        }
        context.Config.MapCircle = false;
    }

    private static void Near(float actual, float expected, string label) => Require(MathF.Abs(actual - expected) < .0001f, $"{label}: {actual}, expected {expected}");
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
