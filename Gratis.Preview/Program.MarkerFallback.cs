using System.Numerics;
using Gratis.Classes;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class Program {
    private static void DrawMarkerFallbackScene(Vector2 frame) {
        var context = MapDrawContext.Current;
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetWindowPos();
        var half = frame.X / 2;
        list.AddRectFilled(origin + new Vector2(10, 80), origin + new Vector2(half, frame.Y - 42),
            ImGui.GetColorU32(new Vector4(.87f, .81f, .64f, 1)));
        list.AddRectFilled(origin + new Vector2(half, 80), origin + frame - new Vector2(10, 42),
            ImGui.GetColorU32(new Vector4(.055f, .075f, .105f, 1)));
        (uint Icon, string Name)[] samples = [(60446, "Exit / door"),
            (60071, "Quest pin"),
            (60501, "FATE"),
            (60412, "Shop")];
        for (var panel = 0; panel < 2; panel++) {
            using var font = context.GetChromeFont(12, ChromeFontRole.Body)?.Push();
            var ink = ImGui.GetColorU32(panel == 0 ? new Vector4(.12f, .1f, .08f, 1) : Vector4.One);
            TextDrawing.Draw(list, origin + new Vector2(panel * half + 24, 98), ink,
                "Icons · Lucide unavailable · vector marks inside contextual plates");
            for (var row = 0; row < 3; row++)
                for (var col = 0; col < samples.Length; col++) {
                    var sample = samples[col];
                    var position = new Vector2(panel * half + 60 + col * (half - 50) / 4, 170 + row * 125);
                    DrawHelpers.DrawMapMarker(new MarkerInfo {
                        IconId = sample.Icon, Position = position, Offset = Vector2.Zero,
                        Scale = row == 0 ? .7f : row == 1 ? 1 : 1.5f
                    });
                    TextDrawing.Draw(list, origin + position + new Vector2(-30, 28), ink, sample.Name);
                }
            TextDrawing.Draw(list, origin + new Vector2(panel * half + 24, frame.Y - 85), ink,
                "0.7x / 1x / 1.5x · same silhouettes, contours and tint · zero bare triangles");
        }
    }
}