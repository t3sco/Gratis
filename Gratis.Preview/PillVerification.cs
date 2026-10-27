using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class PillVerification {
    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException("Pills: " + message);
    }

    public static void Run() {
        var primary = new Vector4(237 / 255f, 237 / 255f, 237 / 255f, 1);
        var warm = new Vector4(.85f, .72f, .48f, 1);
        Require(PillFaces.PaletteRow == new PillProfile {
            Padding = new(12, 3), Inset = new(6, 1), Fill = primary with { W = .055f },
        }, "Palette literals changed.");
        Require(PillFaces.HeadingDetail == new PillProfile {
            Layout = PillLayout.Heading, Padding = new(8, 4), Inset = new(4, 2),
            Fill = warm with { W = .10f }, Ink = new(143 / 255f, 143 / 255f, 143 / 255f, 1),
        }, "Heading literals changed.");
        Require(PillFaces.PinLabel == new PillProfile {
            Padding = new(12, 6), Inset = new(6, 3), Fill = new(0, 0, 0, .95f),
            Ink = primary, Rounding = PillRounding.Control,
        }, "Pin literals changed.");
        Require(PillFaces.RewardMetric == new PillProfile {
            Layout = PillLayout.Metric, Padding = new(20, 0), Height = 30,
            Fill = new(0, 0, 0, .08f), Rounding = PillRounding.Control,
        }, "Metric literals changed.");
        Require(PillFaces.RewardCount == new PillProfile {
            Layout = PillLayout.BottomRight, Padding = new(8, 4), Inset = new(4, 2),
            Fill = new(10 / 255f, 10 / 255f, 11 / 255f, 1), Ink = primary,
            Rounding = PillRounding.Inner, RadiusInset = 2,
        }, "Count literals changed.");
        var dot = new PillProfile {
            Layout = PillLayout.Dot, Inset = new(28, 8), Radius = 2.5f, Fill = warm,
        };
        Require(PillFaces.SettingsDot == dot && PillFaces.FilterDot == dot, "Dot literals changed.");
        foreach (var style in new[] { 0, 1 })
            foreach (var scale in new[] { 1f, 1.25f }) Verify(style, scale);
    }

    private static void Verify(int style, float scale) {
        using var driver = new ScriptedFrameDriver(new SystemConfig {
            AcceptedSpoilerWarning = true, VisualStyle = style, InterfaceAnimations = InterfaceAnimationMode.Off,
            WindowSize = new(600, 700),
        }, new FrameScript { Width = 800, Height = 900 });
        driver.BenchmarkMapVisible = false;
        PillProfile[] faces = [PillFaces.PaletteRow, PillFaces.HeadingDetail, PillFaces.PinLabel,
            PillFaces.RewardMetric, PillFaces.RewardCount, PillFaces.SettingsDot, PillFaces.FilterDot];
        string[] labels = ["Available", "DETAIL", "Pin title", "1,234 gil", "99", "", ""];
        var vertices = new ImDrawVert[7][];
        var indices = new ushort[7][];
        var commands = new (uint Count, Vector4 Clip, uint Vertex, uint Index)[7][];
        var bounds = new Vector4[7];
        var ids = new uint[7];
        var cursors = new Vector2[7];
        bool original = true, capture = false, compare = false, measure = false;
        long bytes = 0;
        driver.DrawPreviewOverlay = () => {
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new(600, 700));
            using var font = driver.Context.GetChromeFont(12 * scale, ChromeFontRole.Chip)?.Push();
            ImGui.Begin("Pill verification", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDecoration);
            for (var i = 0; i < faces.Length; i++) {
                ImGui.PushID(i);
                ImGui.SetCursorScreenPos(new(24, 24 + i * 70));
                ChromeControls.Button("##sentinel", new(48 * scale));
                var pos = new Vector2(100.3f, 24.7f + i * 70);
                ImGui.SetCursorScreenPos(pos);
                ImGui.BeginDisabled(i == 2);
                var list = ImGui.GetWindowDrawList();
                var firstVertex = list.VtxBuffer.Size;
                var firstIndex = list.IdxBuffer.Size;
                var ink = i == 2 ? GratisTheme.Primary with { W = .37f }
                    : i is 0 or 3 ? GratisTheme.WarmAccent : faces[i].Ink;
                var before = GC.GetAllocatedBytesForCurrentThread();
                if (original) Original(i, list, pos, labels[i], ink, scale);
                else Pill.Draw(list, pos, labels[i], faces[i], scale, ink, i == 5 ? .43f : null);
                if (measure) bytes += GC.GetAllocatedBytesForCurrentThread() - before;
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                var rect = new Vector4(min.X, min.Y, max.X, max.Y);
                if (capture) {
                    bounds[i] = rect;
                    ids[i] = ImGui.GetItemID();
                    cursors[i] = ImGui.GetCursorScreenPos();
                    vertices[i] = new ImDrawVert[list.VtxBuffer.Size - firstVertex];
                    indices[i] = new ushort[list.IdxBuffer.Size - firstIndex];
                    for (var v = 0; v < vertices[i].Length; v++) {
                        var actual = list.VtxBuffer[firstVertex + v];
                        vertices[i][v] = new() { pos = actual.pos, uv = actual.uv, col = actual.col };
                    }
                    for (var n = 0; n < indices[i].Length; n++) indices[i][n] = list.IdxBuffer[firstIndex + n];
                    commands[i] = new (uint, Vector4, uint, uint)[list.CmdBuffer.Size];
                    for (var c = 0; c < commands[i].Length; c++) {
                        var cmd = list.CmdBuffer[c];
                        commands[i][c] = (cmd.ElemCount, cmd.ClipRect, cmd.VtxOffset, cmd.IdxOffset);
                    }
                }
                if (compare) {
                    Require(rect == bounds[i] && ImGui.GetItemID() == ids[i], "Native ID/bounds changed.");
                    Require(ImGui.GetCursorScreenPos() == cursors[i], "Native layout changed.");
                    Require(vertices[i].Length == list.VtxBuffer.Size - firstVertex
                        && indices[i].Length == list.IdxBuffer.Size - firstIndex, "Geometry length changed.");
                    for (var v = 0; v < vertices[i].Length; v++) {
                        var expected = vertices[i][v];
                        var actual = list.VtxBuffer[firstVertex + v];
                        Require(expected.pos == actual.pos && expected.uv == actual.uv && expected.col == actual.col,
                            "Vertex order/content changed.");
                    }
                    for (var n = 0; n < indices[i].Length; n++)
                        Require(indices[i][n] == list.IdxBuffer[firstIndex + n], "Index order changed.");
                    Require(commands[i].Length == list.CmdBuffer.Size, "Command count changed.");
                    for (var c = 0; c < commands[i].Length; c++) {
                        var cmd = list.CmdBuffer[c];
                        Require(commands[i][c] == (cmd.ElemCount, cmd.ClipRect, cmd.VtxOffset, cmd.IdxOffset),
                            "Draw commands changed.");
                    }
                }
                ImGui.EndDisabled();
                ImGui.PopID();
            }
            ImGui.End();
        };
        var time = 0d;
        void Step() => driver.Step(new(time += .02), false);
        for (var frame = 0; frame < 64; frame++) Step();
        capture = true;
        Step();
        capture = false;
        original = false;
        for (var frame = 0; frame < 64; frame++) Step();
        compare = true;
        measure = true;
        for (var frame = 0; frame < 256; frame++) Step();
        Require(bytes == 0, $"Steady Draw allocated {bytes} B.");
        Console.WriteLine($"Pills: style {style}, scale {scale}: 7 profiles, IDs/bounds, geometry/commands; {bytes} B / 256 frames.");
    }

    private static void Original(int i, ImDrawListPtr list, Vector2 p, string text, Vector4 ink, float s) {
        if (i >= 5) {
            list.AddCircleFilled(p + new Vector2(28, 8) * s, 2.5f * s,
                ImGui.GetColorU32(i == 5 ? GratisTheme.WarmAccent with { W = .43f } : GratisTheme.WarmAccent));
        }
        else if (i == 0) {
            var size = TextDrawing.Measure(text) + new Vector2(12, 3) * s;
            list.AddRectFilled(p, p + size, ImGui.GetColorU32(GratisTheme.Primary with { W = .055f }),
                GratisTheme.Radius.Pill(size));
            TextDrawing.Draw(list, p + new Vector2(6, 1) * s, ImGui.GetColorU32(ink), text);
        }
        else if (i == 1) {
            var measured = ImGui.CalcTextSize(text);
            list.AddRectFilled(p - new Vector2(4, 2) * s, p + measured + new Vector2(4, 2) * s,
                ImGui.GetColorU32(GratisTheme.WarmAccent with { W = .10f }),
                GratisTheme.Radius.Pill(measured + new Vector2(8, 4) * s));
            ImGui.TextColored(GratisTheme.Secondary, text);
        }
        else if (i == 2) {
            var size = TextDrawing.Measure(text) + new Vector2(12, 6) * s;
            list.AddRectFilled(p, p + size, ImGui.GetColorU32(GratisTheme.Outline with { W = .95f }),
                GratisTheme.Radius.Control * s);
            TextDrawing.Draw(list, p + new Vector2(6, 3) * s, ImGui.GetColorU32(ink), text);
        }
        else if (i == 3) {
            var size = new Vector2(TextDrawing.Measure(text).X + 20 * s, 30 * s);
            list.AddRectFilled(p, p + size, ImGui.GetColorU32(ink with { W = .08f }), GratisTheme.Radius.Control * s);
            TextDrawing.Draw(list, p + (size - TextDrawing.Measure(text)) / 2, ImGui.GetColorU32(ink), text);
            ImGui.Dummy(size);
        }
        else {
            var size = TextDrawing.Measure(text) + new Vector2(8, 4) * s;
            var badge = p - size;
            list.AddRectFilled(badge, p, ImGui.GetColorU32(GratisTheme.Surface with { W = 1 }),
                GratisTheme.Radius.Inner(GratisTheme.Radius.Control * s, 2 * s));
            TextDrawing.Draw(list, badge + new Vector2(4, 2) * s, ImGui.GetColorU32(GratisTheme.Primary), text);
        }
    }
}
