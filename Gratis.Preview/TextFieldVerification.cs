using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class TextFieldVerification {
    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException("Text fields: " + message);
    }

    public static void Run() {
        var primary = new Vector4(237 / 255f, 237 / 255f, 237 / 255f, 1);
        FieldProfile[] expected = [
            new() { Hint = "Search places...", Capacity = 256, Padding = new(8, 0), CenterPadding = true,
                Radius = 4, Fill = new(1, 1, 1, .055f), Ink = primary,
                Clear = FieldClear.Adjacent, ClearWidthDivisor = 3 },
            new() { Hint = "Search quests...", Capacity = 256 },
            new() { Hint = "Find a zone", Capacity = 256, Flags = ImGuiInputTextFlags.EnterReturnsTrue },
            new() { Hint = "Search settings...", Capacity = 128, Padding = new(32, 11),
                IconPosition = new(16, 18), IconSize = 14, Clear = FieldClear.Overlay, ClearSize = 36 },
            new() { Hint = "Search settings", Capacity = 128 },
        ];
        FieldProfile[] actual = [FieldFaces.Palette, FieldFaces.Quest, FieldFaces.Location,
            FieldFaces.Settings, FieldFaces.ClassicSettings];
        Require(actual.SequenceEqual(expected), "Profile literals changed.");
        Verify(InterfaceAnimationMode.Off, 1, 0);
        Verify(InterfaceAnimationMode.Full, 1, 0);
        Verify(InterfaceAnimationMode.Off, 1.25f, 0);
        Verify(InterfaceAnimationMode.Off, 1, 1);
    }

    private readonly record struct InputState(uint Id, Vector4 Bounds, bool Focused, bool Active,
        Vector2 Padding, float Radius, Vector4 Fill, Vector4 Ink, Vector4 Selection);
    private sealed record Capture(InputState Input, FieldResult Result, string Text, Vector2 Cursor,
        ImDrawVert[] Vertices, ushort[] Indices);

    private static void Verify(InterfaceAnimationMode animations, float scale, int style) {
        var (original, before) = Frames(true, animations, scale, style);
        var (extracted, after) = Frames(false, animations, scale, style);
        Require(original.Count == extracted.Count, "Capture count changed.");
        for (var i = 0; i < original.Count; i++) {
            var a = original[i];
            var b = extracted[i];
            Require(a.Input == b.Input && a.Result == b.Result && a.Text == b.Text && a.Cursor == b.Cursor,
                $"Native input, clear button or layout changed at capture {i}.");
            Require(a.Vertices.Length == b.Vertices.Length && a.Indices.SequenceEqual(b.Indices),
                $"Draw topology changed at capture {i}.");
            for (var v = 0; v < a.Vertices.Length; v++)
                Require(a.Vertices[v].pos == b.Vertices[v].pos && a.Vertices[v].uv == b.Vertices[v].uv
                    && a.Vertices[v].col == b.Vertices[v].col, $"Draw vertex changed at capture {i}.");
        }
        Require(after == 0, $"Steady Draw path allocated {after} B.");
        Console.WriteLine($"Text fields: {animations}, scale {scale}, style {style}: {original.Count} captures; "
            + $"original {before} B -> {after} B / 512 empty/populated frames; IDs, geometry, focus, selection, Unicode, "
            + "capacity, Enter, disabled input and clearing passed.");
    }

    private static (List<Capture>, long) Frames(bool original, InterfaceAnimationMode animations, float scale, int style) {
        var config = new SystemConfig { AcceptedSpoilerWarning = true, VisualStyle = style,
            InterfaceAnimations = animations, ChromeScale = scale, WindowSize = new(600, 700) };
        using var driver = new ScriptedFrameDriver(config, new FrameScript { Width = 800, Height = 900 });
        driver.BenchmarkMapVisible = false;
        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        FieldOptions[] options = [new(FieldFaces.Palette, new(320 * scale, 36 * scale), scale),
            new(FieldFaces.Quest), new(FieldFaces.Location), new(FieldFaces.Settings, Scale: scale),
            new(FieldFaces.ClassicSettings)];
        string[] ids = ["##place-query-1", "##quest-log-search", "##location-query-1",
            "##settings-search", "##settings-search"];
        string[] text = ["", "", "", "", ""];
        var results = new FieldResult[5];
        var inputs = new InputState[5];
        var captures = new List<Capture>();
        var input = default(InputState);
        var focus = -1;
        var disabled = -1;
        var focusClear = -1;
        var drawingSite = -1;
        var serial = 1;
        var capture = false;
        var measure = false;
        long allocated = 0;
        void ReadInput(string id) {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var inherited = ImGui.GetStyle();
            input = new(ImGui.GetItemID(), new(min.X, min.Y, max.X, max.Y), ImGui.IsItemFocused(),
                ImGui.IsItemActive(), inherited.FramePadding, inherited.FrameRounding,
                inherited.Colors[(int)ImGuiCol.FrameBg], inherited.Colors[(int)ImGuiCol.Text],
                inherited.Colors[(int)ImGuiCol.TextSelectedBg]);
            Require(input.Id == ImGui.GetID(id), "Native ID changed.");
        }
        TextField.PreviewAfterInput = ReadInput;
        ChromeControls.PreviewBeforeButton = id => {
            if (drawingSite == focusClear && id == "##clear-search") ImGui.SetKeyboardFocusHere();
        };
        driver.DrawPreviewOverlay = () => {
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new(600, 700));
            ImGui.SetNextWindowFocus();
            using var font = driver.Context.GetChromeFont(14 * scale, ChromeFontRole.Body)?.Push();
            ImGui.Begin("Text field verification", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDecoration);
            for (var i = 0; i < options.Length; i++) {
                drawingSite = i;
                ImGui.PushID(i);
                ImGui.SetCursorScreenPos(new(24, 24 + i * 100));
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,
                    (i == 1 ? new Vector2(10, 8) : i == 4 ? new Vector2(8, 5) : new Vector2(8, 6)) * scale);
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6 * scale);
                ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(1, 1, 1, i == 1 ? .055f : .025f));
                ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Primary);
                ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, new Vector4(.2f, .4f, .6f, .35f));
                ImGui.BeginDisabled(i == disabled);
                if (i == focus) ImGui.SetKeyboardFocusHere();
                var list = ImGui.GetWindowDrawList();
                var firstVertex = list.VtxBuffer.Size;
                var firstIndex = list.IdxBuffer.Size;
                var padding = ImGui.GetStyle().FramePadding;
                var radius = ImGui.GetStyle().FrameRounding;
                var fill = ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg];
                var before = GC.GetAllocatedBytesForCurrentThread();
                results[i] = original ? Original(i, i == 2 ? "##location-query-" + serial : ids[i],
                    ref text[i], options[i]) : TextField.Draw(ids[i], ref text[i], options[i]);
                if (measure) allocated += GC.GetAllocatedBytesForCurrentThread() - before;
                inputs[i] = input;
                Require(ImGui.GetStyle().FramePadding == padding && ImGui.GetStyle().FrameRounding == radius
                    && ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg] == fill, "Field style leaked.");
                if (results[i].Cleared) {
                    text[i] = "";
                    if (i == 0) { ids[i] = "##place-query-2"; focus = 0; }
                }
                if (capture) {
                    var vertices = new ImDrawVert[list.VtxBuffer.Size - firstVertex];
                    var indices = new ushort[list.IdxBuffer.Size - firstIndex];
                    for (var v = 0; v < vertices.Length; v++) {
                        var vertex = list.VtxBuffer[firstVertex + v];
                        vertices[v] = new() { pos = vertex.pos, uv = vertex.uv, col = vertex.col };
                    }
                    for (var n = 0; n < indices.Length; n++) indices[n] = list.IdxBuffer[firstIndex + n];
                    captures.Add(new(input, results[i], text[i], ImGui.GetCursorPos(), vertices, indices));
                }
                ImGui.EndDisabled();
                ImGui.PopStyleColor(3);
                ImGui.PopStyleVar(2);
                ImGui.PopID();
            }
            ImGui.End();
        };
        var time = 0d;
        void Step(string? value = null, ImGuiKey[]? keys = null, bool control = false,
            Vector2? mouse = null, bool left = false) =>
            driver.Step(new(time += .02, Text: value, Keys: keys, Control: control,
                Mouse: mouse ?? new(-1000), Left: left), false);
        try {
            for (var frame = 0; frame < 64; frame++) Step();
            capture = true;
            Step();
            capture = false;
            measure = true;
            for (var frame = 0; frame < 256; frame++) Step();
            measure = false;
            capture = true;
            for (var i = 0; i < options.Length; i++) {
                focus = i;
                Step();
                focus = -1;
                Step();
                Require(inputs[i].Focused && inputs[i].Active, "Keyboard focus changed.");
                Step("é漢🙂");
                Require(text[i].StartsWith("é漢", StringComparison.Ordinal), "Unicode input changed.");
                Require(results[i].Changed == (i != 2), "Native change return changed.");
                Step(keys: [ImGuiKey.A], control: true);
                Step(new string('x', (int)options[i].Profile.Capacity + 40));
                Require(text[i].Length == options[i].Profile.Capacity, "Capacity changed.");
                Step(keys: [ImGuiKey.Enter]);
                Require(results[i].Changed == (i == 2), "EnterReturnsTrue changed.");
                Step();
            }
            capture = false;
            for (var frame = 0; frame < 64; frame++) Step();
            measure = true;
            for (var frame = 0; frame < 256; frame++) Step();
            measure = false;
            capture = true;
            disabled = 4;
            var unchanged = text[4];
            Step("ignored");
            Require(text[4] == unchanged, "Disabled input accepted text.");
            disabled = -1;
            foreach (var i in new[] { 0, 3 }) {
                var bounds = results[i].ClearBounds;
                var mouse = new Vector2((bounds.X + bounds.Z) / 2, (bounds.Y + bounds.W) / 2);
                Step(mouse: mouse);
                Step(mouse: mouse, left: true);
                Step(mouse: mouse);
                if (i == 0)
                    Require(text[i] == "" && results[i].Cleared, "Adjacent clear action changed.");
                else {
                    focusClear = i;
                    Step();
                    focusClear = -1;
                    Step();
                    Step(keys: [ImGuiKey.Enter]);
                    Step();
                }
                focus = -1;
                Step();
            }
            text[0] = text[2] = "external";
            ids[0] = "##place-query-3";
            ids[2] = "##location-query-" + ++serial;
            focus = 0;
            Step();
            focus = -1;
            Step();
            Require(text[0] == "external" && text[2] == "external", "Reopened buffer retained stale text.");
            options[0] = new(FieldFaces.Palette, new(12 * scale, 36 * scale), scale);
            Step();
            return (captures, allocated);
        }
        finally { TextField.PreviewAfterInput = null; ChromeControls.PreviewBeforeButton = null; }
    }

    private static FieldResult Original(int site, string id, ref string text, in FieldOptions options) {
        var s = options.Scale;
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        bool changed;
        var clearBounds = Vector4.Zero;
        var cleared = false;
        if (site == 0) {
            var size = options.Size;
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,
                new Vector2(8 * s, MathF.Max(0, (size.Y - ImGui.GetFontSize()) / 2)));
            var radius = GratisTheme.Radius.Inner(GratisTheme.Radius.Card * s, 4 * s);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, radius);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(1, 1, 1, .055f));
            ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Primary);
            var clearSize = MathF.Min(size.Y, size.X / 3);
            ImGui.SetNextItemWidth(MathF.Max(1, size.X - clearSize));
            changed = ImGui.InputTextWithHint(id, "Search places...", ref text, 256);
            TextField.PreviewAfterInput?.Invoke(id);
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(2);
            var clearPos = start + new Vector2(size.X - clearSize, 0);
            ImGui.SetCursorScreenPos(clearPos);
            clearBounds = new(clearPos.X, clearPos.Y, clearPos.X + clearSize, clearPos.Y + size.Y);
            cleared = ChromeControls.IconButton("##clear-search", GratisIcons.X, "Clear search", new(clearSize, size.Y),
                windowBounds: MapToolbarDrawing.WindowBounds, radius: radius);
        }
        else if (site == 3) {
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(32, 11) * s);
            ImGui.SetNextItemWidth(-1);
            changed = ImGui.InputTextWithHint(id, "Search settings...", ref text, 128);
            TextField.PreviewAfterInput?.Invoke(id);
            ImGui.PopStyleVar();
            GratisIcons.Draw(ImGui.GetWindowDrawList(), start + new Vector2(16, 18) * s,
                GratisIcons.Search, GratisTheme.Secondary, 14 * s);
            if (text.Length > 0) {
                var next = ImGui.GetCursorPos();
                var clearPos = start + new Vector2(width - 36 * s, 0);
                ImGui.SetCursorScreenPos(clearPos);
                clearBounds = new(clearPos.X, clearPos.Y, clearPos.X + 36 * s, clearPos.Y + 36 * s);
                cleared = ChromeControls.IconButton("##clear-search", GratisIcons.X, "Clear search", new Vector2(36) * s, s);
                ImGui.SetCursorPos(next);
                ImGui.Dummy(Vector2.Zero);
            }
        }
        else {
            ImGui.SetNextItemWidth(-1);
            changed = site == 1 ? ImGui.InputTextWithHint(id, "Search quests...", ref text, 256)
                : site == 2 ? ImGui.InputTextWithHint(id, "Find a zone", ref text, 256, ImGuiInputTextFlags.EnterReturnsTrue)
                : ImGui.InputTextWithHint(id, "Search settings", ref text, 128);
            TextField.PreviewAfterInput?.Invoke(id);
        }
        return new(changed, cleared, clearBounds);
    }
}
