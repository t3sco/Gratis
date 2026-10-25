using System.Numerics;
using Gratis.Classes;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class Program
{
    private static void DrawLegibilityScene(string scene, Vector2 frame)
    {
        var context = MapDrawContext.Current;
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetWindowPos();
        var pale = scene == "markers-parchment";
        var paper = pale ? new Vector4(.87f, .81f, .64f, 1) : new Vector4(.055f, .075f, .105f, 1);
        list.AddRectFilled(origin + new Vector2(10, 80), origin + frame - new Vector2(10, 40), ImGui.GetColorU32(paper));
        for (var x = 14; x < frame.X - 10; x += 23)
            list.AddLine(origin + new Vector2(x, 80), origin + new Vector2(x, frame.Y - 40),
                ImGui.GetColorU32(new Vector4(pale ? .3f : .8f, .55f, .4f, .07f)));
        for (var y = 95; y < frame.Y - 40; y += 37)
            list.AddLine(origin + new Vector2(10, y), origin + new Vector2(frame.X - 10, y),
                ImGui.GetColorU32(new Vector4(.45f, .35f, .2f, .08f)));
        var ink = ImGui.GetColorU32(pale ? new Vector4(.12f, .10f, .08f, 1) : Vector4.One);
        using (context.GetChromeFont(12, ChromeFontRole.Body)?.Push())
            TextDrawing.Draw(list, origin + new Vector2(30, 92), ink,
                "100% opaque terrain  |  marker scales: 0.7x / 1.0x / 1.5x  |  dark contour + light keyline");
        (uint Icon, string Name)[] samples = [(60071, "Objective ?"), (71021, "Quest !"), (71203, "MSQ ?"),
            (71145, "Blue quest"), (60501, "FATE"), (60453, "Aetheryte")];
        for (var row = 0; row < 3; row++) {
            var scale = new[] { .7f, 1f, 1.5f }[row];
            for (var col = 0; col < samples.Length; col++) {
                var position = new Vector2(80 + col * (frame.X - 110) / samples.Length, 151 + row * 78);
                var sample = samples[col];
                DrawHelpers.DrawMapMarker(new MarkerInfo { Position = position, Offset = Vector2.Zero,
                    Scale = scale, IconId = sample.Icon, PrimaryText = () => sample.Name });
                using var font = context.GetChromeFont(11, ChromeFontRole.Body)?.Push();
                TextDrawing.Draw(list, origin + position + new Vector2(-30, 24), ink, sample.Name);
            }
        }
        var lineY = 365f;
        foreach (var size in new[] { 12f, 16f, 21f, 28f, 40f }) {
            DrawHelpers.DrawText(new MarkerInfo { Position = new(frame.X * .37f, lineY), Offset = Vector2.Zero,
                Scale = size / 96, IconId = 0 }, $"Government Sector · {size:0}px");
            lineY += size + 10;
        }
        for (var i = 0; i < 24; i++)
            DrawHelpers.DrawMapMarker(new MarkerInfo { Position = new(frame.X - 140 + i % 3, 465 + i % 4),
                Offset = Vector2.Zero, Scale = 1, IconId = 61000, PrimaryText = () => "Ambient cluster" });
        context.Motion.Trail.Clear();
        for (var i = 0; i < 40; i++)
            context.Motion.Trail.Add(new(frame.X - 200 + i * 3, 530 + MathF.Sin(i / 8f) * 18), ImGui.GetTime() - 3.9 + i * .1, 4);
        context.Motion.DrawTrail(origin, 1, ImGui.GetTime());
    }
}
