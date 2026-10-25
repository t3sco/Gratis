using System.Numerics;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class GlassVerification
{
    public static void Run(PreviewContext context)
    {
        var c = context.Config;
        c.VisualStyle = 0; c.GlassEnabled = c.GlassBlur = true; c.GlassBlurRadius = 12;
        context.BlurAvailable = true;
        for (var frame = 0; frame < 2; frame++) {
            ImGui.NewFrame();
            ImGui.SetNextWindowPos(new Vector2(28, 24)); ImGui.SetNextWindowSize(new Vector2(264, 192));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            ImGui.Begin("glass verification", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings);
            ImGui.PopStyleVar();
            var list = ImGui.GetWindowDrawList();
            list.PushClipRect(new Vector2(38, 34), new Vector2(282, 206), true);
            context.Blurs.Clear();
            context.Blur.BeginFrame(ImGui.GetFrameCount());
            list.AddRectFilled(new Vector2(38, 34), new Vector2(282, 206), 0xFFFFFFFF);
            var filmIndex = list.IdxBuffer.Size;
            MapWindowDrawing.DrawSurface(list, new Vector2(28, 48), new Vector2(148, 104), 8, surface: GlassSurface.Toolbar);
            MapWindowDrawing.DrawSurface(list, new Vector2(180, 48), new Vector2(240, 108), 30, surface: GlassSurface.Title);
            MapWindowDrawing.DrawSurface(list, new Vector2(300, 48), new Vector2(340, 104), 8);
            MapWindowDrawing.DrawSurface(list, new Vector2(28, 128), new Vector2(78, 178), 25);
            MapWindowDrawing.DrawSurface(list, new Vector2(80, 128), new Vector2(148, 178), 8, alpha: 0, surface: GlassSurface.Coordinates);
            c.GlassEnabled = false;
            MapWindowDrawing.DrawSurface(list, new Vector2(80, 128), new Vector2(148, 178), 8);
            c.GlassEnabled = true; context.BlurAvailable = false;
            MapWindowDrawing.DrawSurface(list, new Vector2(180, 128), new Vector2(248, 178), 8);
            context.BlurAvailable = true;
            Require(context.Blurs.Count == 2 && context.Blur.Blurred == 2 && context.Blur.Refused == 5, "Frame blur counts");
            foreach (var reason in Enum.GetValues<BlurRefusal>()) Require(context.Blur.Count(reason) == 1, $"Refusal {reason}");
            Require(context.Blur.SurfaceStatus(GlassSurface.Toolbar) == "blurred" && context.Blur.SurfaceStatus(GlassSurface.Title) == "blurred" &&
                context.Blur.SurfaceStatus(GlassSurface.Coordinates) == "alpha", "Individual chrome status");
            Require(context.Blurs[0].Min == new Vector2(38, 48) && context.Blurs[0].Max == new Vector2(148, 104), "Partial surface clips instead of refusing");
            Require(context.Blurs[0].IndexOffset == filmIndex && filmIndex < list.IdxBuffer.Size, "Blur follows backdrop and precedes film");
            Require(context.Blurs[1].Rounding == 30, "Full rounded square supports a circular mask");
            Require(MapWindowDrawing.DrawWindowBackgroundBlur(list, new Vector2(38, 34), new Vector2(282, 206), 8, true, 3), "Window blur records");
            Require(context.Blurs[^1].IndexOffset == 0, "Window blur precedes the child background/map");
            list.PopClipRect(); ImGui.End(); ImGui.Render();
        }
        var diagnostics = new BlurDiagnostics();
        diagnostics.Failed(new InvalidOperationException("temporary\nrecording failure"), 20);
        Require(diagnostics.BlurUnavailable && !diagnostics.CanAttempt(139) && diagnostics.CanAttempt(140), "Retry cooldown");
        diagnostics.Succeeded();
        Require(!diagnostics.BlurUnavailable && diagnostics.LastException.Contains("temporary recording failure"), "Recovery retains last exception");
        Require(BlurDiagnostics.Strength(12, 1) == 3 && BlurDiagnostics.Strength(12, .5f) == 1.5f &&
            BlurDiagnostics.Strength(48, 1) == 8 && BlurDiagnostics.Strength(.01f, 1) == .05f, "Host strength mapping and limits");
        VerifyChromeContrast(c);
        VerifyToolbar(context);
        var json = new global::System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        var legacy = global::System.Text.Json.JsonSerializer.Deserialize<Gratis.Data.SystemConfig>(
            "{\"VisualStyle\":1,\"GlassEnabled\":false,\"GlassBlur\":false,\"BlurBackground\":true,\"BlurStrength\":4.5,\"GlassTintOpacity\":0.02,\"UnknownOldField\":true}", json)!;
        Require(!legacy.IsGratis && !legacy.GlassEnabled && !legacy.GlassBlur && legacy.BlurBackground &&
            legacy.BlurStrength == 4.5f && legacy.GlassTintOpacity == .02f, "Explicit legacy settings survive");
        var defaults = global::System.Text.Json.JsonSerializer.Deserialize<Gratis.Data.SystemConfig>("{}", json)!;
        Require(defaults.GlassEnabled && defaults.GlassBlur && defaults.GlassBlurRadius == 12 && defaults.GlassTintOpacity == .04f &&
            defaults.GlassHighlight == .5f && !defaults.GlassGrain && !defaults.BlurBackground && defaults.BlurStrength == 3, "Missing settings inherit glass defaults");
        Console.WriteLine("Verified glass clipping, circular masks/fallback, refusal counts, per-surface status, parent-list toolbar glass, draw order, strength, retry recovery, contrast and legacy settings.");
    }

    private static unsafe void VerifyToolbar(PreviewContext context)
    {
        var c = context.Config;
        c.PinChrome = true; c.MapCircle = false; c.HideWindowFrame = true;
        c.GlassTintOpacity = .04f; c.GlassGrain = false;
        foreach (var segmented in new[] { true, false }) for (var frame = 0; frame < 2; frame++) {
            c.SegmentedToolbar = segmented;
            ImGui.GetIO().AddMousePosEvent(-1000, -1000);
            ImGui.NewFrame();
            ImGui.SetNextWindowPos(new Vector2(28, 24)); ImGui.SetNextWindowSize(new Vector2(264, 192));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            ImGui.Begin("toolbar glass verification", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings);
            ImGui.PopStyleVar();
            Gratis.Classes.MapShape.Update(ImGui.GetWindowSize());
            context.Chrome.Update(c, ImGui.GetTime(), false, 1);
            var parent = (nint)ImGui.GetWindowDrawList().NativePtr;
            ImGui.SetCursorPos(Vector2.Zero);
            context.Blurs.Clear();
            MapToolbarDrawing.Draw(_ => { }, () => { });
            if (frame == 1) {
                Require(context.Blur.SurfaceStatus(GlassSurface.Toolbar) == "blurred" && context.Blurs.Count == 1 &&
                    context.Blurs[0].List == parent, $"{(segmented ? "Pill" : "Standard")} glass uses the map list");
                if (segmented) Require(context.Blurs[0].Max - context.Blurs[0].Min == new Vector2(3 * GratisTheme.ControlHeight + 8, GratisTheme.ToolbarHeight), "Full pill rim escapes the child inset clip");
            }
            ImGui.End(); ImGui.Render();
        }
        c.SegmentedToolbar = true;
    }

    private static void VerifyChromeContrast(MapDrawingConfig c)
    {
        float Linear(float value) => value <= .04045f ? value / 12.92f : MathF.Pow((value + .055f) / 1.055f, 2.4f);
        c.GlassGrain = true;
        foreach (var tint in new[] { 0f, .02f, .08f, .2f }) foreach (var grain in new[] { 0f, .018f, .04f, .08f }) {
            c.GlassTintOpacity = tint; c.GlassGrainIntensity = grain;
            var channel = 1 - FrameMaterialDrawing.GlassChromeOpacity(c) * (1 - GratisTheme.Surface.Z);
            channel += (1 - channel) * FrameMaterialDrawing.GlassTint(c);
            channel += (1 - channel) * FrameMaterialDrawing.GlassGrain(c);
            channel = MathF.Ceiling(channel * 255) / 255;
            Require((Linear(.78f) + .05f) / (Linear(channel) + .05f) >= 4.5f, "Chrome text contrast over white");
        }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
