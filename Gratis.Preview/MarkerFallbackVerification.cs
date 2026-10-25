using System.Numerics;
using Gratis.Classes;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class MarkerFallbackVerification {
    private static void Require(bool condition, string message) {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    public static void Run() {
        uint[] icons = [60446, 60414, 60071, 71025, 71031, 60412, 60501, 60453, 60442, 61000];
        var scene = new FrameScript();
        for (var i = 0; i < icons.Length; i++)
            scene.Markers.Add(new(icons[i], new(-160 + i % 5 * 80, -50 + i / 5 * 100)));
        var config = new SystemConfig {
            AcceptedSpoilerWarning = true, FadeMode = 0, FollowPlayer = false,
            Markers = new() { IconStyle = MarkerIconStyle.Icons },
            MapScale = 1, WindowPosition = new(32), WindowSize = new(640, 480)
        };
        using var driver = new ScriptedFrameDriver(config, scene);
        var context = driver.Context;
        var paths = context.MarkerRendering;
        context.IconFontsEnabled = false;
        var frame = driver.Step(new(0));
        Require(frame.MarkersDrawn == icons.Length, "Missing atlas must not hide any marker");
        Require(paths.Count(MarkerDrawPath.PlateFallback) == 8 && paths.Count(MarkerDrawPath.VectorPlate) == 1
            && paths.Count(MarkerDrawPath.Shape) == 0 && paths.Count(MarkerDrawPath.GlyphPlate) == 0,
            "Icons with no font must use plates, never bare category shapes");
        Require(paths.Diagnostics().Contains("handle=unavailable/pending"),
            "Fallback diagnostics must identify unavailable handles");

        ImGui.NewFrame();
        ImGui.Begin("Atlas policy verification");
        var marker = new MarkerInfo { IconId = 60446, Position = Vector2.Zero, Offset = Vector2.Zero, Scale = 1 };
        var first = FlatMarkerDrawing.Symbol(ImGui.GetWindowDrawList(), new(100), 8, marker);
        context.IconFontsEnabled = true;
        var second = FlatMarkerDrawing.Symbol(ImGui.GetWindowDrawList(), new(140, 100), 8, marker);
        Require(first == MarkerDrawPath.PlateFallback && second == first,
            "Atlas policy must stay fixed within the frame");
        ImGui.End();
        ImGui.Render();
        driver.Step(new(.02));
        Require(paths.Count(MarkerDrawPath.GlyphPlate) == 8 && paths.Count(MarkerDrawPath.VectorPlate) == 1
            && paths.Count(MarkerDrawPath.PlateFallback) == 0,
            "A ready atlas must recover contextual glyphs on the next frame");

        context.MissingIconGlyph = GratisIcons.DoorOpen;
        driver.Step(new(.04));
        Require(paths.Count(MarkerDrawPath.PlateFallback) == 8 && paths.Count(MarkerDrawPath.Shape) == 0
            && paths.Count(MarkerDrawPath.GlyphPlate) == 0,
            "One missing glyph selects vector plates for the entire frame");
        Require(paths.Diagnostics().Contains("missingGlyph=U+E3D6") && paths.Diagnostics().Contains("family=Exit")
            && paths.Diagnostics().Contains("handle=ready"),
            "Diagnostics must distinguish missing glyphs from pending fonts");
        context.MissingIconGlyph = '\0';

        ImGui.NewFrame();
        ImGui.Begin("Icon lookup verification");
        foreach (var pixels in new[] { 8.1f, 13.37f, 15.99f, 24.8f, 31.6f }) {
            var font = context.GetIconFont(pixels);
            Require(ReferenceEquals(font, context.GetIconFont(TextDrawing.IconPixelSize(pixels))),
                "Fractional sizes must reuse the snapped cached icon handle");
            using var scope = font!.Push();
            Require(ImGui.GetFont().FontSize == TextDrawing.IconPixelSize(pixels),
                "Lookup must return the nearest built native icon size");
        }
        _ = context.GetIconFont(13.37f);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
            _ = context.GetIconFont(13.37f);
        Require(GC.GetAllocatedBytesForCurrentThread() == allocated, "Cached icon-size lookups must allocate nothing");
        ImGui.End();
        ImGui.Render();

        config.Markers.IconStyle = MarkerIconStyle.Shapes;
        driver.Step(new(.06));
        Require(paths.Count(MarkerDrawPath.Shape) == 9 && paths.Count(MarkerDrawPath.PlateFallback) == 0,
            "Only Shapes mode draws category shapes");
        config.Markers.IconStyle = MarkerIconStyle.Game;
        driver.Step(new(.08));
        Require(paths.Count(MarkerDrawPath.GameIcon) == icons.Length && paths.Count(MarkerDrawPath.Shape) == 0,
            "Game mode retains textures and records their path");
        config.VisualStyle = 1;
        config.Markers.IconStyle = MarkerIconStyle.Icons;
        driver.Step(new(.10));
        Require(paths.Count(MarkerDrawPath.GameIcon) == icons.Length && paths.Count(MarkerDrawPath.PlateFallback) == 0,
            "Classic retains its original icon path");
        Console.WriteLine("Marker fallback: absent/partial/recovered atlases, frame policy, visibility, "
            + "cached fractional lookups (0 B), Shapes/Game/Classic and diagnostics passed.");
    }
}