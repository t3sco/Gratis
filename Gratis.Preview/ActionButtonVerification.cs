using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class ActionButtonVerification {
    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException("Action buttons: " + message);
    }

    public static void Run() {
        Profiles();
        foreach (var style in new[] { 0, 1 }) SteadyState(style);
        Console.WriteLine("Action buttons: nine literal profiles, original native IDs/bounds, disabled/dimmed semantics; "
            + "0 B across 256 steady frames per style (including fitted labels).");
    }

    private static void Profiles() {
        var secondary = new Vector4(143 / 255f, 143 / 255f, 143 / 255f, 1);
        var primary = new Vector4(237 / 255f, 237 / 255f, 237 / 255f, 1);
        var crystal = new Vector4(.72f, .9f, 1, 1);
        var gold = new Vector4(.85f, .72f, .48f, 1);
        var dark = new Vector4(10 / 255f, 10 / 255f, 11 / 255f, 1);
        var quiet = new ActionProfile {
            Height = 36, WidthPadding = 16, Radius = 6, TextInset = 8, Ink = secondary,
            Feedback = ActionFeedback.Fill, Control = true,
        };
        var action = new ActionProfile {
            Height = 36, WidthPadding = 38, Radius = 6, TextInset = 27, IconInset = 13, IconSize = 14,
            Fill = crystal with { W = .20f }, HoverFill = crystal with { W = .30f },
            Border = crystal with { W = .45f },
            Ink = primary, IconInk = crystal, Feedback = ActionFeedback.Outline,
            Control = true, IconBeforeFeedback = true,
        };
        var travel = new ActionProfile {
            Height = 36, WidthPadding = 40, Radius = 6, TextInset = 30, IconInset = 15, IconSize = 18,
            Fill = crystal with { W = .94f }, HoverFill = crystal, Ink = dark, IconInk = dark,
            Feedback = ActionFeedback.Outline, Control = true,
        };
        var teleport = new ActionProfile {
            Height = 36, Radius = 6, TextInset = 14, TrailingInset = 14, TrailingGap = 12,
            Fill = crystal with { W = .20f }, HoverFill = crystal with { W = .30f },
            Border = crystal with { W = .45f }, Ink = primary, TrailingInk = crystal,
            Feedback = ActionFeedback.Outline, Label = ActionLabel.Fit, Control = true, DisabledScope = true,
        };
        (string Name, ActionProfile Actual, ActionProfile Expected)[] table = [
            ("Quiet", ActionFaces.Quiet, quiet), ("Action", ActionFaces.Action, action),
            ("PrimaryTravel", ActionFaces.PrimaryTravel, travel),
            ("Send", ActionFaces.Send, travel with { Disabled = ActionDisabled.Dim, DisabledAlpha = .4f }),
            ("Travel", ActionFaces.Travel, teleport),
            ("ConfirmTravel", ActionFaces.ConfirmTravel, teleport with {
                Fill = crystal with { W = .38f }, HoverFill = crystal with { W = .50f },
                Border = crystal with { W = .85f },
            }),
            ("Update", ActionFaces.Update, new() {
                Height = 36, HeightInset = 8, WidthPadding = 24, Radius = 8, RadiusInset = 4, TextInset = 12,
                Fill = crystal with { W = .20f }, HoverFill = crystal with { W = .30f },
                Border = crystal with { W = .45f }, Ink = primary,
                Tooltip = "Opens the plugin installer so you can update Gratis", WindowTooltip = true,
            }),
            ("QuestMap", ActionFaces.QuestMap, new() {
                Height = 36, Radius = 6, Ink = gold, Feedback = ActionFeedback.Selected, Label = ActionLabel.Center,
                DisabledScope = true, Tooltip = "Show on map  M / Shift+Enter",
            }),
            ("NativeText", ActionFaces.NativeText, new() {
                Radius = 6, Label = ActionLabel.Native, Feedback = ActionFeedback.Fill, Control = true,
            }),
        ];
        foreach (var row in table)
            Require(row.Actual == row.Expected, row.Name + " literals changed.");
    }

    private static void SteadyState(int style) {
        var config = new SystemConfig { AcceptedSpoilerWarning = true, VisualStyle = style,
            InterfaceAnimations = InterfaceAnimationMode.Full, WindowSize = new(600, 700) };
        using var driver = new ScriptedFrameDriver(config, new FrameScript { Width = 800, Height = 900 });
        driver.BenchmarkMapVisible = false;
        var fitted = new ActionLabelCache();
        ActionOptions[] options = [
            new(ActionFaces.Quiet), new(ActionFaces.Action, Icon: GratisIcons.Compass),
            new(ActionFaces.PrimaryTravel, Icon: GratisIcons.Check),
            new(ActionFaces.Send, Icon: GratisIcons.Check, Enabled: false),
            new(ActionFaces.Travel, TrailingText: "1,234 gil", Enabled: false, Size: new(220, 36), LabelCache: fitted),
            new(ActionFaces.ConfirmTravel, Size: new(160, 36), LabelCache: new()),
            new(ActionFaces.Update), new(ActionFaces.QuestMap, Enabled: false, Size: new(220, 36)),
            new(ActionFaces.NativeText),
        ];
        string[] labels = ["Cancel", "Travel here", "Teleport", "Send", "Teleport", "Pay 1,234 gil and teleport",
            "Update", "No map destination", "Native action"];
        string[] ids = ["Cancel", "Travel here", "Teleport", "Send", "##travel-action", "##confirm-action",
            "##update-action", "##quest-detail-map", "Native action"];
        long bytes = 0;
        var measure = false;
        var hoverIndex = -1;
        var focusSend = false;
        var focusedSend = false;
        driver.DrawPreviewOverlay = () => {
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new(600, 700));
            ImGui.SetNextWindowFocus();
            ImGui.Begin("Action profile verification", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDecoration);
            for (var i = 0; i < options.Length; i++) {
                ImGui.SetCursorScreenPos(new(24, 24 + i * 50));
                var expectedId = ImGui.GetID(ids[i]);
                var inherited = ImGui.GetStyle().Alpha;
                var before = GC.GetAllocatedBytesForCurrentThread();
                if (i == 3 && focusSend) ImGui.SetKeyboardFocusHere();
                ActionButton.Draw(ids[i], labels[i], options[i]);
                if (measure) bytes += GC.GetAllocatedBytesForCurrentThread() - before;
                if (i != 8 || style == 0)
                    Require(UiMotion.Item == expectedId, "Native ID changed.");
                Require(ImGui.GetItemRectMin() == new Vector2(24, 24 + i * 50), "Hit origin changed.");
                var expected = options[i].Size != default ? options[i].Size
                    : i == 8 ? ImGui.CalcTextSize(labels[i]) + ImGui.GetStyle().FramePadding * 2
                    : new Vector2(ImGui.CalcTextSize(labels[i]).X + options[i].Profile.WidthPadding,
                        options[i].Profile.Height - options[i].Profile.HeightInset);
                Require(ImGui.GetItemRectSize() == expected, "Hit size changed.");
                Require(ImGui.GetStyle().Alpha == inherited, "Disabled/dimmed alpha leaked.");
                if (i == 3) focusedSend = ImGui.IsItemFocused();
                if (i == hoverIndex) {
                    Require(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled), "Disabled hit rectangle changed.");
                    Require(ImGui.IsItemHovered() == (i == 3), "Send/native disabled hover semantics changed.");
                }
                if (i != 8 || style == 0) {
                    Require(UiMotion.Store.Contains(new(expectedId, 3)) == (options[i].Profile.Control && !UiMotion.Off),
                        "Press transform channel changed.");
                    Require(UiMotion.Store.Contains(new(expectedId, 7)) == (options[i].Profile.Feedback != ActionFeedback.None && !UiMotion.Off),
                        "Focus feedback channel changed.");
                }
            }
            ImGui.End();
        };
        for (var frame = 0; frame < 64; frame++) driver.Step(new(1 + frame / 60d), false);
        measure = true;
        for (var frame = 0; frame < 256; frame++) driver.Step(new(3 + frame / 60d), false);
        Require(bytes == 0, $"steady Draw path allocated {bytes} B for style {style}.");
        measure = false;
        foreach (var index in new[] { 3, 4, 7 }) {
            hoverIndex = index;
            driver.Step(new(8 + index, Mouse: new Vector2(40, 35 + index * 50)), false);
        }
        hoverIndex = -1;
        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        focusSend = true;
        driver.Step(new(16), false);
        focusSend = false;
        driver.Step(new(16.04), false);
        Require(focusedSend, "Dimmed Send lost keyboard focus.");
    }
}
