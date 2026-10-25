using System.Numerics;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class FontSafetyVerification {
    private static void Check(bool value, string message) {
        if (!value)
            throw new InvalidOperationException(message);
    }

    public static void Run() {
        var budget = new FontAtlasBudget(FontAtlasBudget.MaximumTextHandles, FontAtlasBudget.MaximumArea / 2);
        for (var i = 0; i < 10000; i++)
            budget.Reserve(10, TextDrawing.PixelSize(i % 140), FontAtlasBudget.TextGlyphs);
        var icons = new FontAtlasBudget();
        foreach (var size in TextDrawing.IconSizes)
            icons.Reserve(100, size, GratisIcons.GlyphRanges.Length / 2);
        Check(budget.Count == 7 && budget.EstimatedArea + icons.EstimatedArea < FontAtlasBudget.MaximumArea / 2
            && budget.Denied == 0,
            "All roles/sizes must share seven Geist handles, stay below 50% including unchanged icons, and never deny");
        var retry = new FontAtlasBudget();
        var creations = 0;
        for (var frame = 0; frame < 10000; frame++)
            if (retry.Reserve(1, 14, FontAtlasBudget.TextGlyphs))
                creations++;
        Check(creations == 1 && retry.Count == 1, "A failed creation cannot allocate another handle on the next frame");
        var handleLimit = new FontAtlasBudget();
        for (var face = 0; face < 100; face++)
            handleLimit.Reserve(face, 14, 0);
        Check(handleLimit.Count == FontAtlasBudget.MaximumHandles,
            "Handle cap must hold independently of estimated area");
        Check(TextDrawing.Sizes.Length == 7 && TextDrawing.Sizes.Max() == 24 && TextDrawing.PixelSize(128) == 24
            && TextDrawing.IconPixelSize(48) == 48,
            "Large text must reuse 24px; icon inventory stays unchanged");
        var bounds = new Vector4(0, 0, 9, 14);
        var uv = new Vector4(.1f, .1f, .2f, .2f);
        Check(FontAtlasBudget.ValidGlyph(14, bounds, uv), "Sane glyph metrics must be accepted");
        Check(!FontAtlasBudget.ValidGlyph(float.NaN, bounds, uv)
            && !FontAtlasBudget.ValidGlyph(14, bounds, new(0, 0, 2, 2))
            && !FontAtlasBudget.ValidGlyph(14, new(0, 0, 900, 900), uv),
            "Invalid metrics/UVs must be rejected");
        using var driver = new ScriptedFrameDriver();
        driver.Step(new(0));
        var cached = driver.Context.GetChromeFont(14, ChromeFontRole.Body);
        for (var frame = 0; frame < 1000; frame++)
            Check(ReferenceEquals(cached, driver.Context.GetChromeFont(14, ChromeFontRole.Body)),
                "Repeated text lookups must reuse the wrapper");
        foreach (var role in Enum.GetValues<ChromeFontRole>()) {
            driver.Config.FrameFont = (int)role;
            Check(ReferenceEquals(cached, driver.Context.GetChromeFont(14, role))
                && ReferenceEquals(cached, driver.Context.GetTitleFont((int)role, 14)),
                "Every saved font choice and role must map to one regular face");
        }
        Check(driver.Context.TextBudget.Count == 7 && driver.Context.TextDiagnostics().Contains("denied=0"),
            "Harness must expose a bounded, non-denying cache");
        ImGui.NewFrame();
        ImGui.Begin("Font safety");
        using (driver.Context.GetChromeFont(14, ChromeFontRole.Body)!.Push()) {
            Check(DrawBinding.ValidTextFont(ImGui.GetFont()), "Valid Geist text atlas must pass sentinel validation");
            Check(TextDrawing.Measure("11.1 / 11.1", tabularDigits: true) == TextDrawing.Measure("88.8 / 88.8",
                tabularDigits: true),
                "Coordinates must use fixed digit cells in regular Geist");
            Check(TextDrawing.Measure("ET 11:11", tabularDigits: true) == TextDrawing.Measure("ET 08:08",
                tabularDigits: true),
                "Time digit cells must not change width");
            TextDrawing.Draw(ImGui.GetWindowDrawList(), new(40), 0xffffffff, "ET 08:08 · 11.1 / 88.8",
                tabularDigits: true);
        }
        using (driver.Context.GetIconFont(14)!.Push())
            Check(!DrawBinding.ValidTextFont(ImGui.GetFont()),
                "A Lucide/PUA atlas must never be accepted as a text font");
        driver.Context.TextFontsReady = false;
        using (driver.Context.GetChromeFont(48, ChromeFontRole.Title)!.Push()) {
            Check(ImGui.GetFont().FontSize == 18 && DrawBinding.ValidTextFont(ImGui.GetFont()),
                "Pending sized fonts must use the default UI font");
            TextDrawing.Draw(ImGui.GetWindowDrawList(), new(30), 0xffffffff, "Settings A m 0 ?");
        }
        ImGui.End();
        ImGui.Render();
        Console.WriteLine("Font safety: 10,000 requests, seven shared regular Geist sizes, area including "
            + $"icons={budget.EstimatedArea + icons.EstimatedArea}/{FontAtlasBudget.MaximumArea}, "
            + "denied=0, 24px cap, fixed digit cells, glyph validation and fallback passed.");
    }
}