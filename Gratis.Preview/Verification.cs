using System.Numerics;
using Gratis.Classes;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class Verification
{
    private const int Width = 320, Height = 240;
    private static readonly Vector4 Background = new Vector4(22, 25, 31, 255) / 255;

    public static void Run(PreviewContext context, Textures textures)
    {
        ImGui.GetIO().DisplaySize = new Vector2(Width, Height);
        var c = context.Config;
        c.VisualStyle = 0; c.FeatherEnabled = true; c.NeutralFrame = true;
        var bytes = new byte[32 * 32 * 4];
        for (var y = 0; y < 32; y++) for (var x = 0; x < 32; x++) {
            var index = (y * 32 + x) * 4;
            bytes[index] = (byte)(x * 8); bytes[index + 1] = (byte)(y * 8);
            bytes[index + 2] = 128; bytes[index + 3] = 255;
        }
        var id = textures.Add(32, 32, bytes);
        var texture = new DrawTexture(id, new Vector2(256));
        var cases = 0;
        foreach (var circle in new[] { false, true })
        foreach (var radius in new[] { 0f, 10, 64 })
        foreach (var feather in new[] { .5f, 6, 48 })
        foreach (var zoom in new[] { .75f, 2 }) {
            c.MapCircle = circle; c.FlatCornerRadius = radius; c.FeatherWidth = feather;
            Begin();
            var min = ImGui.GetWindowPos(); var max = min + ImGui.GetWindowSize();
            MapShape.Update(max - min);
            var imageMin = min - new Vector2(30, 40);
            if (cases == 0) for (var i = 0; i < 16380; i++)
                ImGui.GetWindowDrawList().AddRectFilled(new Vector2(-100), new Vector2(-99), 0xFFFFFFFF);
            ImGui.SetCursorScreenPos(imageMin);
            MapTextureDrawing.DrawMapImage(texture, zoom);
            End();
            var renderer = new SoftwareRenderer(Width, Height, textures);
            renderer.Render(ImGui.GetDrawData());
            var rounding = circle ? 96 : radius;
            var half = (max - min) / 2;
            for (var y = 0; y < Height; y++) for (var x = 0; x < Width; x++) {
                var p = new Vector2(x + .5f, y + .5f);
                var q = Vector2.Abs(p - (min + max) / 2) - half + new Vector2(rounding);
                var depth = circle ? 96 - Vector2.Distance(p, (min + max) / 2)
                    : rounding - Vector2.Max(q, Vector2.Zero).Length() - MathF.Min(MathF.Max(q.X, q.Y), 0);
                var uv = (p - imageMin) / (texture.Size * zoom);
                var t = Math.Clamp(depth / feather, 0, 1);
                var alpha = t * t * (3 - 2 * t);
                if (uv.X < 0 || uv.Y < 0 || uv.X >= 1 || uv.Y >= 1) alpha = 0;
                var expected = Vector4.Lerp(Background, textures.Get(id).Sample(uv), alpha);
                var delta = Vector4.Abs(expected - renderer.Pixel(x, y));
                var tolerance = depth >= feather + .2f || depth < -.2f ? .0005f : .025f + .16f / feather;
                Require(MathF.Max(delta.X, MathF.Max(delta.Y, delta.Z)) <= tolerance,
                    $"Mask/UV mismatch at {x},{y}: circle={circle}, radius={radius}, feather={feather}, zoom={zoom}, delta={delta}.");
            }
            Require(FeatheredMapDrawing.LastVertexCount <= 4645, "Map vertex cap exceeded.");
            cases++;
        }
        VerifyBlur(context, textures);
        VerifyContrast(c);
        c.FeatherEnabled = false; c.FeatherWidth = 48; c.GlassBlur = false; c.GlassBlurRadius = 48;
        c.ResetFrameMaterials();
        Require(c.FeatherEnabled && c.FeatherWidth == 6 && c.GlassBlur && c.GlassBlurRadius == 12, "Material reset failed.");
        Console.WriteLine($"Verified {cases} analytic mask/UV cases (including partial images and 16-bit vertex rollover), blur structure, contrast and material reset.");
    }

    private static void VerifyBlur(PreviewContext context, Textures textures)
    {
        context.Blurs.Clear();
        Begin();
        var list = ImGui.GetWindowDrawList();
        for (var y = 24; y < 216; y += 4) for (var x = 28; x < 292; x += 4)
            list.AddRectFilled(new Vector2(x, y), new Vector2(x + 4, y + 4), ((x + y) / 4 % 2) == 0 ? 0xFFFFFFFF : 0xFF000000);
        context.TryBlurBehind(list, new Vector2(80, 64), new Vector2(240, 176), 12, 12);
        End();
        var raw = new SoftwareRenderer(Width, Height, textures); raw.Render(ImGui.GetDrawData());
        var blurred = new SoftwareRenderer(Width, Height, textures); blurred.Render(ImGui.GetDrawData(), context.Blurs);
        var beforeEnergy = 0f; var afterEnergy = 0f; var beforeMean = 0f; var afterMean = 0f; var samples = 0;
        for (var y = 0; y < Height; y++) for (var x = 0; x < Width; x++) {
            var a = raw.Pixel(x, y).X; var b = blurred.Pixel(x, y).X;
            if (x < 80 || x >= 240 || y < 64 || y >= 176 || x == 80 && y == 64)
                Require(a == b, "Blur escaped its rounded mask.");
            if (x < 96 || x >= 224 || y < 80 || y >= 160) continue;
            var da = a - raw.Pixel(x - 1, y).X; var db = b - blurred.Pixel(x - 1, y).X;
            beforeEnergy += da * da; afterEnergy += db * db;
            beforeMean += a; afterMean += b; samples++;
        }
        Require(blurred.BlurCount == 1 && afterEnergy < beforeEnergy * .02f, "Blur failed to reduce high-frequency energy.");
        Require(MathF.Abs(beforeMean - afterMean) / samples < .02f, "Blur changed region mean by over 2%.");
        Console.WriteLine($"CPU box blur: energy ratio {afterEnergy / beforeEnergy:F6}, mean delta {MathF.Abs(beforeMean - afterMean) / samples:F6}; outside mask unchanged.");
    }

    private static void VerifyContrast(MapDrawingConfig c)
    {
        float Linear(float value) => value <= .04045f ? value / 12.92f : MathF.Pow((value + .055f) / 1.055f, 2.4f);
        var worst = float.MaxValue;
        c.GlassGrain = true;
        foreach (var tint in new[] { 0f, .02f, .08f, .2f }) foreach (var grain in new[] { 0f, .018f, .04f, .08f }) {
            c.GlassTintOpacity = tint; c.GlassGrainIntensity = grain;
            var backing = FrameMaterialDrawing.GlassBackingOpacity(c);
            var channel = 1 - backing * (1 - GratisTheme.Surface.Z);
            channel += (1 - channel) * FrameMaterialDrawing.GlassTint(c);
            channel += (1 - channel) * FrameMaterialDrawing.GlassGrain(c);
            channel = MathF.Ceiling(channel * 255) / 255;
            var ratio = (Linear(GratisTheme.Secondary.X) + .05f) / (Linear(channel) + .05f);
            worst = MathF.Min(worst, ratio);
            Require(ratio >= 4.5f, $"Glass contrast is only {ratio:F3}:1.");
        }
        Console.WriteLine($"Settled glass secondary-text contrast >= {worst:F2}:1, including saved out-of-range tint/grain values.");
    }

    private static void Begin()
    {
        ImGui.NewFrame();
        ImGui.SetNextWindowPos(new Vector2(28, 24)); ImGui.SetNextWindowSize(new Vector2(264, 192));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        ImGui.Begin("material verification", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings);
        ImGui.PopStyleVar(2);
    }

    private static void End() { ImGui.End(); ImGui.Render(); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
