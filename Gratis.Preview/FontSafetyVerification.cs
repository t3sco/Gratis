using System.Numerics;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class FontSafetyVerification
{
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public static void Run()
    {
        var budget = new FontAtlasBudget();
        for (var i = 0; i < 10000; i++) budget.Reserve(i % 8, TextDrawing.PixelSize(i % 140), FontAtlasBudget.TextGlyphs);
        Check(budget.Count <= 16 && budget.EstimatedArea <= FontAtlasBudget.MaximumArea && budget.Denied > 0,
            "Font stress must stay inside handle and atlas area budgets");
        var retry = new FontAtlasBudget();
        var creations = 0;
        for (var frame = 0; frame < 10000; frame++)
            if (retry.Reserve(1, 14, FontAtlasBudget.TextGlyphs)) creations++;
        Check(creations == 1 && retry.Count == 1,
            "A failed creation cannot allocate another handle on the next frame");
        var handleLimit = new FontAtlasBudget();
        for (var face = 0; face < 100; face++) handleLimit.Reserve(face, 14, 0);
        Check(handleLimit.Count == FontAtlasBudget.MaximumHandles, "Handle cap must hold independently of estimated area");
        Check(TextDrawing.Sizes.Length == 11 && TextDrawing.Sizes.Max() == 48 && TextDrawing.PixelSize(128) == 48,
            "Large labels must reuse the capped 48px font");
        var bounds = new Vector4(0, 0, 9, 14); var uv = new Vector4(.1f, .1f, .2f, .2f);
        Check(FontAtlasBudget.ValidGlyph(14, bounds, uv), "Sane glyph metrics must be accepted");
        Check(!FontAtlasBudget.ValidGlyph(float.NaN, bounds, uv) && !FontAtlasBudget.ValidGlyph(14, bounds, new(0, 0, 2, 2)) &&
            !FontAtlasBudget.ValidGlyph(14, new(0, 0, 900, 900), uv), "Invalid metrics/UVs must be rejected");
        using var driver = new ScriptedFrameDriver();
        driver.Step(new(0));
        var cached = driver.Context.GetChromeFont(14, ChromeFontRole.Body);
        for (var frame = 0; frame < 1000; frame++)
            Check(ReferenceEquals(cached, driver.Context.GetChromeFont(14, ChromeFontRole.Body)), "Repeated text lookups must reuse the wrapper");
        ImGui.NewFrame(); ImGui.Begin("Font safety");
        using (driver.Context.GetChromeFont(14, ChromeFontRole.Body)!.Push())
            Check(DrawBinding.ValidTextFont(ImGui.GetFont()), "Valid Geist text atlas must pass sentinel validation");
        using (driver.Context.GetIconFont(14)!.Push())
            Check(!DrawBinding.ValidTextFont(ImGui.GetFont()), "A Lucide/PUA atlas must never be accepted as a text font");
        driver.Context.TextFontsReady = false;
        using (driver.Context.GetChromeFont(48, ChromeFontRole.Title)!.Push()) {
            Check(ImGui.GetFont().FontSize == 18 && DrawBinding.ValidTextFont(ImGui.GetFont()), "Pending sized fonts must use the default UI font");
            TextDrawing.Draw(ImGui.GetWindowDrawList(), new(30), 0xffffffff, "Settings A m 0 ?");
        }
        ImGui.End(); ImGui.Render();
        Console.WriteLine("Font safety: 10,000 requests, 16-handle/area limits, no failed-create retry, 48px cap, metrics/UV/PUA rejection and default-font fallback passed.");
    }
}
