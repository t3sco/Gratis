using System.Numerics;
using Gratis.Classes;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class ChromeVerification
{
    public static void Run(PreviewContext context)
    {
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
        Console.WriteLine("Verified chrome delay/easing/minima, reveal/pin/Classic/reset, all 18 Lucide glyphs at 12/16/24px, zero-alpha surface/blur, and first-hover toolbar clicks.");
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
            for (var step = 0; step < 4; step++) {
                var mouse = step < 2 ? new Vector2(-1000) : new Vector2(54, 50);
                io.AddMousePosEvent(mouse.X, mouse.Y);
                io.AddMouseButtonEvent(0, step == 2);
                ImGui.NewFrame();
                ImGui.SetNextWindowPos(new Vector2(28, 24));
                ImGui.SetNextWindowSize(new Vector2(264, 192));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                ImGui.Begin("chrome input verification", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings);
                ImGui.PopStyleVar();
                MapShape.Update(ImGui.GetWindowSize());
                if (step == 0) context.Chrome.Reveal(ImGui.GetTime() - 2);
                context.Chrome.Update(c, ImGui.GetTime(), ImGui.IsMouseHoveringRect(ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize(), false));
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
            Require(activations == 1, $"First-hover follow click dispatched {activations} times");
        }
        finally { io.ConfigInputTrickleEventQueue = trickle; }
    }

    private static void Near(float actual, float expected, string label) => Require(MathF.Abs(actual - expected) < .0001f, $"{label}: {actual}, expected {expected}");
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
