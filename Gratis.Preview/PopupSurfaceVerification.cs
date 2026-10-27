using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class PopupSurfaceVerification {
    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException("Popup surfaces: " + message);
    }

    public static void Run() {
        var native = new PopupProfile {
            Radius = 8, BorderSize = 0, Background = new(10 / 255f, 10 / 255f, 11 / 255f, 1),
        };
        Require(PopupFaces.Native == native, "Native literals changed.");
        Require(PopupFaces.Chrome == native with {
            Controls = true, Padding = new(12), ChildRadius = 8, FrameRadius = 6, GrabInset = 2,
            FramePadding = new(10, 7), ItemSpacing = new(8, 4), Border = new(1, 1, 1, .10f),
            Ink = new(237 / 255f, 237 / 255f, 237 / 255f, 1),
            DisabledInk = new(143 / 255f, 143 / 255f, 143 / 255f, 1),
            Fill = new(1, 1, 1, .04f), HoverFill = new(1, 1, 1, .08f), ActiveFill = new(1, 1, 1, .12f),
        }, "Chrome literals changed.");
        foreach (var style in new[] { 0, 1 })
            foreach (var scale in new[] { 1f, 1.25f }) Verify(style, scale);
    }

    private static void Verify(int visualStyle, float chromeScale) {
        var config = new SystemConfig { AcceptedSpoilerWarning = true, VisualStyle = visualStyle,
            ChromeScale = chromeScale, InterfaceAnimations = InterfaceAnimationMode.Full };
        using var driver = new ScriptedFrameDriver(config);
        driver.BenchmarkMapVisible = false;
        var verify = true;
        var measure = false;
        long originalBytes = 0, bytes = 0;
        driver.DrawPreviewOverlay = () => {
            ImGui.Begin("Popup surface verification", ImGuiWindowFlags.NoSavedSettings);
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, .63f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 23);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 29);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 3);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 5);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(31, 37));
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 41);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 43);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(47, 53));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(59, 61));
            ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 67);
            ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 71);
            ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 18);
            for (var disabled = 0; disabled < 2; disabled++) {
                ImGui.BeginDisabled(disabled == 1);
                for (var chrome = 0; chrome < 2; chrome++)
                    for (var explicitScale = 0; explicitScale < 2; explicitScale++)
                        for (var transparent = 0; transparent < (chrome == 1 ? 2 : 1); transparent++) {
                            float? scale = explicitScale == 1 ? 2 : null;
                            var face = chrome == 1 ? PopupFaces.Chrome : PopupFaces.Native;
                            if (verify) VerifyOrder(face, scale, transparent == 1);
                            var inherited = ImGui.GetStyle().Alpha;
                            var before = GC.GetAllocatedBytesForCurrentThread();
                            var counts = Original(chrome == 1, transparent == 1, scale);
                            ImGui.PopStyleColor(counts.Colors);
                            ImGui.PopStyleVar(counts.Variables);
                            if (measure) originalBytes += GC.GetAllocatedBytesForCurrentThread() - before;
                            before = GC.GetAllocatedBytesForCurrentThread();
                            using (PopupSurface.Push(face, scale, transparent == 1)) { }
                            if (measure) bytes += GC.GetAllocatedBytesForCurrentThread() - before;
                            Require(ImGui.GetStyle().Alpha == inherited, "Inherited alpha changed.");
                        }
                ImGui.EndDisabled();
            }
            ImGui.PopStyleVar(13);
            ImGui.End();
        };
        for (var frame = 0; frame < 64; frame++) {
            driver.Step(new(1 + frame / 60d), false);
            verify = false;
        }
        measure = true;
        for (var frame = 0; frame < 256; frame++) driver.Step(new(3 + frame / 60d), false);
        Require(bytes == 0, $"steady Push/Dispose allocated {bytes} B.");
        Console.WriteLine($"Popup surfaces: style {visualStyle}, chrome scale {chromeScale}: original {originalBytes} B "
            + $"-> {bytes} B / 256 frames; literal profiles, ordered stacks, nested native fill, "
            + "explicit/inherited scale, transparency and disabled alpha passed.");
    }

    private static void VerifyOrder(PopupProfile face, float? scale, bool transparent) {
        var inherited = Capture();
        var counts = Original(face.Controls, transparent, scale);
        var expected = new Snapshot[counts.Colors + counts.Variables + 1];
        expected[0] = Capture();
        for (var i = 0; i < counts.Colors; i++) {
            ImGui.PopStyleColor();
            expected[i + 1] = Capture();
        }
        for (var i = 0; i < counts.Variables; i++) {
            ImGui.PopStyleVar();
            expected[counts.Colors + i + 1] = Capture();
        }
        PopupSurface.Push(face, scale, transparent);
        Match(expected[0]);
        for (var i = 0; i < counts.Colors; i++) {
            ImGui.PopStyleColor();
            Match(expected[i + 1]);
        }
        for (var i = 0; i < counts.Variables; i++) {
            ImGui.PopStyleVar();
            Match(expected[counts.Colors + i + 1]);
        }
        Match(inherited);
        var scope = PopupSurface.Push(face, scale, transparent);
        Match(expected[0]);
        using (PopupSurface.Push(PopupFaces.Native, scale)) {
            Require(ImGui.GetStyle().Colors[(int)ImGuiCol.PopupBg] == PopupFaces.Native.Background,
                "Nested native background changed.");
            Require(ImGui.GetStyle().Alpha == inherited.Variables[0], "Nested native alpha changed.");
        }
        Match(expected[0]);
        scope.Dispose();
        Match(inherited);
    }

    private readonly record struct Snapshot(float[] Variables, Vector4[] Colors);

    private static Snapshot Capture() {
        var s = ImGui.GetStyle();
        float[] variables = [s.Alpha, s.WindowPadding.X, s.WindowPadding.Y, s.WindowRounding,
            s.PopupRounding, s.WindowBorderSize, s.PopupBorderSize, s.ChildRounding,
            s.FrameRounding, s.FramePadding.X, s.FramePadding.Y, s.ItemSpacing.X, s.ItemSpacing.Y,
            s.GrabRounding, s.ScrollbarRounding, s.ScrollbarSize];
        var colors = new Vector4[(int)ImGuiCol.COUNT];
        for (var i = 0; i < colors.Length; i++) colors[i] = s.Colors[i];
        return new(variables, colors);
    }

    private static void Match(Snapshot expected) {
        var actual = Capture();
        Require(expected.Variables.SequenceEqual(actual.Variables), "Style variable stack changed.");
        Require(expected.Colors.SequenceEqual(actual.Colors), "Style color stack changed.");
    }

    private static (int Variables, int Colors) Original(bool chrome, bool transparent, float? scale) {
        if (!chrome) {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, GratisTheme.Radius.Popup * (scale ?? GratisTheme.ChromeScale));
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, GratisTheme.Radius.Popup * (scale ?? GratisTheme.ChromeScale));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 0);
            ImGui.PushStyleColor(ImGuiCol.PopupBg, GratisTheme.Surface with { W = 1 });
            ImGui.PushStyleColor(ImGuiCol.WindowBg, GratisTheme.Surface with { W = 1 });
            return (4, 2);
        }
        var gratis = UiMotion.IsGratis;
        var s = gratis ? scale ?? GratisTheme.ChromeScale : GratisTheme.ChromeScale;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, GratisTheme.Radius.Popup * s);
        if (gratis) {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, GratisTheme.Radius.Popup * s);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, GratisTheme.Radius.Card * s);
            ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, GratisTheme.Radius.Inner(GratisTheme.Radius.Control * s, 2 * s));
            ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, GratisTheme.Radius.Pill(new(ImGui.GetStyle().ScrollbarSize)));
        }
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, GratisTheme.Radius.Control * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 7) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 4) * s);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, transparent ? Vector4.Zero : GratisTheme.Surface with { W = 1 });
        ImGui.PushStyleColor(ImGuiCol.WindowBg, transparent ? Vector4.Zero : GratisTheme.Surface with { W = 1 });
        ImGui.PushStyleColor(ImGuiCol.Border, GratisTheme.Border);
        ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Primary);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, GratisTheme.Secondary);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(1, 1, 1, .04f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(1, 1, 1, .08f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(1, 1, 1, .12f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(1, 1, 1, .04f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(1, 1, 1, .08f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(1, 1, 1, .12f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1, 1, 1, .04f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1, 1, 1, .08f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1, 1, 1, .12f));
        return (gratis ? 11 : 6, 14);
    }
}
