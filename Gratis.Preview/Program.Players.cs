using System.Numerics;
using Gratis.Classes;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class Program
{
    private static void DrawPlayersScene(Vector2 frame)
    {
        var context = MapDrawContext.Current;
        var origin = ImGui.GetWindowPos();
        var list = ImGui.GetWindowDrawList();
        using var font = context.GetChromeFont(14, ChromeFontRole.Title)?.Push();
        void Label(Vector2 unit, string text)
        {
            var position = origin + frame * unit;
            var bounds = ImGui.CalcTextSize(text);
            list.AddRectFilled(position - new Vector2(6, 3), position + bounds + new Vector2(6, 3),
                ImGui.GetColorU32(new Vector4(.04f, .07f, .1f, .9f)), 4);
            list.AddText(position, ImGui.GetColorU32(Vector4.One), text);
        }
        Label(new Vector2(.08f, .16f), "Players · dots / self arrow only");
        var samples = new[] {
            (new Vector2(.22f, .35f), 60421u, "Party", "Alisaie", "RDM · Lv. 100"),
            (new Vector2(.48f, .35f), 60403u, "Alliance", "G'raha Tia", "PLD · Lv. 100"),
            (new Vector2(.72f, .35f), 60444u, "Nearby / friend", "Tataru", "ACN · Lv. 90"),
        };
        foreach (var (position, id, label, name, job) in samples) {
            DrawHelpers.DrawMapMarker(new MarkerInfo {
                Position = frame * position, Offset = Vector2.Zero, Scale = 1, IconId = id,
                PrimaryText = () => name, SecondaryText = () => job,
            });
            Label(position + new Vector2(-.04f, .04f), label);
        }
        Label(new Vector2(.38f, .76f), "Self · size 1.00");
        Label(new Vector2(.67f, .76f), "Self · size 0.35");
        var small = origin + frame * new Vector2(.73f, .63f);
        var saved = context.Config.PlayerIconScale;
        try {
            context.Config.PlayerIconScale = .35f;
            PlayerDrawing.DrawLookLine(small, .8f, -1.25f);
            PlayerDrawing.DrawPlayerIcon(small, context.GetIcon(60443), .25f, 1);
        }
        finally { context.Config.PlayerIconScale = saved; }

        if (ImGui.GetFrameCount() == 1) {
            var probe = origin + frame / 2;
            list.PushClipRect(Vector2.Zero, Vector2.Zero, true);
            float ArrowWidth(float setting)
            {
                context.Config.PlayerIconScale = setting;
                var start = list.VtxBuffer.Size;
                PlayerDrawing.DrawPlayerIcon(probe, context.GetIcon(60443), .25f, 1);
                var min = float.MaxValue; var max = float.MinValue;
                for (var i = start; i < list.VtxBuffer.Size; i++) {
                    min = Math.Min(min, list.VtxBuffer[i].pos.X);
                    max = Math.Max(max, list.VtxBuffer[i].pos.X);
                }
                return max - min;
            }
            try {
                var full = ArrowWidth(1);
                var shrunk = ArrowWidth(.35f);
                if (!(shrunk > full * .2f && shrunk < full * .5f))
                    throw new InvalidOperationException("Saved player size must shrink the emitted arrow and outline.");
                Console.WriteLine($"Players: emitted arrow width {full:F2}px -> {shrunk:F2}px at size 0.35; no player cross path.");
            }
            finally { context.Config.PlayerIconScale = saved; list.PopClipRect(); }
        }
    }
}
