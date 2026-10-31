using System.Numerics;
using System.Text.Json;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;
using ImGuiNET;

namespace Gratis.Preview;

internal static class QuickSetupVerification {
    private static void Require(bool value, string message) {
        if (!value)
            throw new InvalidOperationException("Quick setup: " + message);
    }

    internal static SystemConfig Fresh(int width = 800, int height = 660) {
        var config = new SystemConfig {
            AcceptedSpoilerWarning = true, FadeMode = 0, PinChrome = true, ReduceMotion = true, FollowPlayer = false,
            WindowPosition = new(16), WindowSize = new(width - 32, height - 32), MapScale = .45f,
        };
        config.MarkFreshInstall();
        return config;
    }

    private sealed class Rig(SystemConfig config, int width = 800, int height = 660) : IDisposable {
        public readonly SystemConfig Config = config;
        public readonly ScriptedFrameDriver Driver = new(config, new FrameScript { Width = width, Height = height });
        private double time;
        public QuickSetup Setup => Driver.Context.QuickSetup;
        public void Idle(int frames = 3, bool? pvp = null, bool? busy = null, bool? focused = null) {
            for (var i = 0; i < frames; i++)
                Driver.Step(new(time += .05, Mouse: new(-1000), Left: false, IsPvP: i == 0 ? pvp : null,
                    Busy: i == 0 ? busy : null, Focused: i == 0 ? focused : null));
        }

        public void Click(string label) {
            var b = Setup.BoundsOf(label);
            Require(b.Z > b.X && b.W > b.Y, $"'{label}' is not on the {Setup.Step} step");
            ClickAt(new Vector2((b.X + b.Z) / 2, (b.Y + b.W) / 2));
        }

        public void ClickAt(Vector2 at) {
            Driver.Step(new(time += .05, Mouse: at, Left: false));
            Driver.Step(new(time += .05, Mouse: at, Left: true));
            Driver.Step(new(time += .05, Mouse: at, Left: false));
            Idle(2);
        }

        public void Key(params ImGuiKey[] keys) => Press(false, keys);
        public void ShiftKey(params ImGuiKey[] keys) => Press(true, keys);
        private void Press(bool shift, ImGuiKey[] keys) {
            Driver.Step(new(time += .05, Mouse: new(-1000), Keys: keys, Shift: shift));
            Driver.Step(new(time += .05, Keys: [], Shift: false));
            Idle(1);
        }

        public void Esc() {
            Driver.Step(new(time += .05, Mouse: new(-1000), Escape: true));
            Driver.Step(new(time += .05, Escape: false));
            Idle(2);
        }

        public bool Has(string label) => Setup.BoundsOf(label) != Vector4.Zero;
        public void Dispose() => Driver.Dispose();
    }

    private static SystemConfig Json(string text) => JsonSerializer.Deserialize<SystemConfig>(text,
        ScriptedFrameDriver.Json)!;

    public static void Frames() {
        FreshInstall();
        Gates();
        Steps();
        Looks();
        Behaviours();
        Replay();
        Keyboard();
        Classic();
        Cost();
    }

    private static void FreshInstall() {
        Require(new SystemConfig().QuickSetupDone, "a default config counts as done");
        Require(Json("{}").QuickSetupDone, "an empty config file counts as done");
        Require(Json("{\"AcceptedSpoilerWarning\":true,\"MapScale\":0.75,\"VisualStyle\":0,\"FadeMode\":0,"
            + "\"Markers\":{\"IconStyle\":2}}").QuickSetupDone, "a populated old config counts as done");
        var serialised = JsonSerializer.Serialize(new SystemConfig(), ScriptedFrameDriver.Json);
        Require(Json(serialised).QuickSetupDone, "a saved current config counts as done");
        var fresh = new SystemConfig();
        fresh.MarkFreshInstall();
        Require(!fresh.QuickSetupDone && !Json(JsonSerializer.Serialize(fresh, ScriptedFrameDriver.Json)).QuickSetupDone,
            "a fresh install is pending and stays pending once saved");
        foreach (var old in new[] { Json("{}"), Json("{\"MapScale\":0.5,\"QuestLogPinned\":true}") }) {
            old.AcceptedSpoilerWarning = true;
            old.FadeMode = 0;
            using var rig = new Rig(old);
            rig.Idle(8);
            Require(!rig.Setup.IsOpen && !rig.Setup.Visible, "an existing config never starts Quick setup");
        }
    }

    private static void Gates() {
        var warning = Fresh();
        warning.AcceptedSpoilerWarning = false;
        using (var rig = new Rig(warning)) {
            rig.Idle(6);
            Require(!rig.Setup.IsOpen, "not before the spoiler warning is accepted");
            warning.AcceptedSpoilerWarning = true;
            rig.Idle(4);
            Require(rig.Setup.IsOpen && rig.Setup.Visible && rig.Setup.Step == QuickSetupStep.Welcome,
                "starts at Welcome once the warning is accepted");
            Require(!warning.QuickSetupDone, "the flag stays pending while it is open");
        }
        using (var rig = new Rig(Fresh())) {
            rig.Idle(4, pvp: true);
            Require(!rig.Setup.IsOpen, "never in PvP");
            rig.Idle(4, pvp: false, busy: true);
            Require(!rig.Setup.IsOpen, "never on a loading screen or in a duty");
            rig.Idle(4, busy: false);
            Require(rig.Setup.IsOpen && rig.Setup.Visible, "it starts when PvP and loading are over");
            rig.Idle(2, busy: true);
            Require(rig.Setup.IsOpen && !rig.Setup.Visible && rig.Setup.BoundsOf("Next") != Vector4.Zero,
                "a duty hides it without ending it");
            rig.Esc();
            Require(rig.Setup.IsOpen, "keys do nothing while it is hidden");
            rig.Idle(2, busy: false);
            Require(rig.Setup.Visible, "it comes back after the duty");
        }
    }

    private static void Steps() {
        var config = Fresh();
        using var rig = new Rig(config);
        rig.Idle(4);
        var setup = rig.Setup;
        Require(rig.Has("Next") && rig.Has(QuickSetup.SkipLabel) && !rig.Has("Back"), "Welcome offers Next and Skip only");
        rig.Click("Next");
        Require(setup.Step == QuickSetupStep.Look && rig.Has("Back") && rig.Has(QuickSetup.SkipLabel), "Next opens Look");
        rig.Click("Back");
        Require(setup.Step == QuickSetupStep.Welcome, "Back returns to Welcome");
        rig.Click("Next");
        rig.Click("Next");
        Require(setup.Step == QuickSetupStep.Behaviours && rig.Has(QuickSetup.SkipLabel), "Look leads to Behaviours");
        rig.Click("Next");
        Require(setup.Step == QuickSetupStep.Done && rig.Has("Finish") && rig.Has("Back"), "Behaviours leads to Done");
        rig.Click("Back");
        Require(setup.Step == QuickSetupStep.Behaviours, "Back from Done");
        Require(!config.QuickSetupDone, "still pending mid-flow");
        rig.Click(QuickSetup.SkipLabel);
        Require(!setup.IsOpen && config.QuickSetupDone, "Skip closes it and counts as done");
        rig.Idle(20);
        Require(!setup.IsOpen, "it never reopens by itself");

        var esc = Fresh();
        using (var escRig = new Rig(esc)) {
            escRig.Idle(4);
            escRig.Click("Next");
            escRig.Esc();
            Require(!escRig.Setup.IsOpen && esc.QuickSetupDone, "Esc skips from any step");
            escRig.Idle(10);
            Require(!escRig.Setup.IsOpen, "and stays closed");
        }
        var finish = Fresh();
        using (var done = new Rig(finish)) {
            done.Idle(4);
            for (var i = 0; i < 3; i++)
                done.Click("Next");
            done.Click("Finish");
            Require(!done.Setup.IsOpen && finish.QuickSetupDone, "Finish sets the flag");
            done.Idle(20);
            Require(!done.Setup.IsOpen, "a finished flow never reopens");
        }
    }

    private static void Looks() {
        var defaults = new SystemConfig();
        var config = Fresh();
        config.IconScale = .8f;
        using var rig = new Rig(config);
        rig.Idle(4);
        rig.Click("Next");
        var setup = rig.Setup;
        Require(QuickSetupLooks.All.Count == 3 && QuickSetupLooks.All.All(l => l.Data.StartsWith(ProfileCodec.Prefix)
            && ProfileCodec.TryDecode(l.Data, out _, out _)), "the looks are encoded GRATIS1 profiles");
        Require(QuickSetupLooks.Current(config) == QuickSetupLooks.Vanilla && !rig.Has("Undo"),
            "Vanilla+ is the starting look and nothing is undoable yet");
        rig.Click("Gratis glass");
        Require(config.FlatCornerRadius == 48 && config.GlassEnabled && config.GlassBlur
            && config.FramePreset == FramePreset.Hairline && config.AccentColor == defaults.AccentColor
            && config.IsGratis && config.IconScale == .8f, "Gratis glass applies its profile and leaves other settings");
        Require(QuickSetupLooks.Current(config) == QuickSetupLooks.Glass && rig.Has("Undo"), "the card follows the settings");
        rig.Click("Undo");
        Require(config.FlatCornerRadius == defaults.FlatCornerRadius && !rig.Has("Undo"), "Undo restores the step");
        rig.Click("Gratis glass");
        rig.Click("Vanilla+");
        Require(config.FlatCornerRadius == defaults.FlatCornerRadius && config.IconScale == .8f,
            "Vanilla+ puts the look settings back to default");
        rig.Click("Gratis icons");
        Require(config.Markers.IconStyle == MarkerIconStyle.Icons, "Gratis icons sets the marker style");
        rig.Click("Game icons");
        Require(config.Markers.IconStyle == MarkerIconStyle.Game, "Game icons is the default");
        rig.Click("Gratis icons");
        rig.Click("Gratis glass");
        rig.Click("Undo");
        Require(config.Markers.IconStyle == MarkerIconStyle.Game && config.FlatCornerRadius == 12,
            "Undo covers the look and the marker icons together");
        rig.Click("Classic");
        Require(!config.IsGratis && QuickSetupLooks.Current(config) == QuickSetupLooks.Classic && rig.Has("Undo"),
            "Classic switches the visual style");
        rig.Click("Next");
        Require(setup.Step == QuickSetupStep.Done, "Classic skips the behaviours step");
        rig.Click("Back");
        Require(setup.Step == QuickSetupStep.Look, "Back from Done returns to Look under Classic");
        rig.Click("Vanilla+");
        Require(config.IsGratis && config.FlatCornerRadius == 12, "a Gratis look leaves Classic");
        rig.Click("Gratis glass");
        rig.Click(QuickSetup.SkipLabel);
        Require(config.FlatCornerRadius == 12 && config.QuickSetupDone && config.IconScale == .8f,
            "Skip, use defaults puts the step back");
        // A look picked and confirmed with Next stays.
        var kept = Fresh();
        using var again = new Rig(kept);
        again.Idle(4);
        again.Click("Next");
        again.Click("Gratis glass");
        again.Click("Next");
        again.Click(QuickSetup.SkipLabel);
        Require(kept.FlatCornerRadius == 48, "Skipping later keeps an earlier step's choice");
    }

    private static void Behaviours() {
        var config = Fresh();
        using var rig = new Rig(config);
        rig.Idle(4);
        rig.Click("Next");
        rig.Click("Next");
        var defaults = new SystemConfig();
        var names = QuickSetup.Behaviours.Select(b => b.Label).ToArray();
        Require(rig.Setup.Step == QuickSetupStep.Behaviours && names.All(rig.Has), "four toggles on the Behaviours step");
        Require(!config.QuestLogReplaceJournal && config.TravelActions == defaults.TravelActions
            && config.AutoHideChrome == defaults.AutoHideChrome && config.GhostMode == defaults.GhostMode,
            "every toggle starts at the current default, and the Journal replacement is off");
        var journal = QuickSetup.Behaviours[1].Label;
        rig.Click(journal);
        Require(config.QuestLogReplaceJournal, "the Journal replacement turns on only when chosen");
        rig.Click(journal);
        Require(!config.QuestLogReplaceJournal, "and turns off again");
        rig.Click(names[0]);
        rig.Click(names[2]);
        rig.Click(names[3]);
        Require(config.TravelActions != defaults.TravelActions && config.AutoHideChrome != defaults.AutoHideChrome
            && config.GhostMode != defaults.GhostMode, "the other toggles bind to their settings");
        rig.Click("Undo");
        Require(config.TravelActions == defaults.TravelActions && config.AutoHideChrome == defaults.AutoHideChrome
            && config.GhostMode == defaults.GhostMode && !config.QuestLogReplaceJournal, "Undo puts the page back");
        rig.Click(journal);
        rig.Click("Next");
        rig.Click("Finish");
        Require(config.QuestLogReplaceJournal && config.QuickSetupDone, "a confirmed choice is kept on finish");
        // Resets and reloads treat these as ordinary settings.
        var reset = SettingsLayout.Entries.Single(e => e.Path == "QuestLogReplaceJournal");
        reset.Reset(config, defaults);
        Require(!config.QuestLogReplaceJournal, "the settings reset still works on a quick setup choice");
    }

    private static void Replay() {
        var config = Fresh();
        using var rig = new Rig(config);
        rig.Idle(4);
        rig.Esc();
        Require(config.QuickSetupDone && !rig.Setup.IsOpen, "closed for good");
        var entry = SettingsLayout.Entries.Single(e => e.Path == "QuickSetupDone");
        Require(entry.Section == "Appearance" && entry.Group == "Quick setup" && entry.Label == "Run Quick setup again",
            "the replay entry sits under Appearance");
        config.QuickSetupDone = false;
        entry.Reset(config, new SystemConfig());
        SettingsLayout.Reset(config, "Appearance");
        Require(!config.QuickSetupDone, "Reset section never wipes the Quick setup flag");
        config.QuickSetupDone = true;
        Require(!ProfileCodec.Portable.Any(e => e.Path == "QuickSetupDone"), "profiles never carry the flag");
        var button = Vector4.Zero;
        rig.Driver.PreserveWindowZOrder = true;
        rig.Driver.DrawPreviewOverlay = () => {
            ImGui.SetNextWindowPos(new Vector2(560, 560));
            ImGui.SetNextWindowSize(new Vector2(220, 80));
            ImGui.Begin("replay-test", ImGuiWindowFlags.NoSavedSettings);
            SettingsQuickSetupDrawing.Draw(config);
            var (min, max) = (ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
            button = new(min.X, min.Y, max.X, max.Y);
            ImGui.End();
        };
        rig.Idle(3);
        Require(button.Z > button.X, "the Settings row draws a button");
        rig.ClickAt(new Vector2((button.X + button.Z) / 2, (button.Y + button.W) / 2));
        rig.Driver.DrawPreviewOverlay = null;
        rig.Driver.PreserveWindowZOrder = false;
        rig.Idle(3);
        Require(rig.Setup.IsOpen && rig.Setup.Step == QuickSetupStep.Welcome && config.QuickSetupDone,
            "Run Quick setup again opens it even though the flag says done");
        rig.Esc();
        Require(!rig.Setup.IsOpen && config.QuickSetupDone, "replaying leaves the flag alone");
    }

    private static void Keyboard() {
        var config = Fresh();
        using var rig = new Rig(config);
        rig.Idle(4);
        var setup = rig.Setup;
        Require(setup.FocusedLabel == "Next", "focus starts on the main action");
        rig.Idle(1, focused: false);
        rig.Key(ImGuiKey.Enter);
        Require(setup.Step == QuickSetupStep.Welcome, "keys are ignored while the map is not focused");
        rig.Idle(1, focused: true);
        rig.Key(ImGuiKey.Enter);
        Require(setup.Step == QuickSetupStep.Look, "Enter presses the focused button");
        string Walk(ImGuiKey key, bool shift = false, string? until = null) {
            for (var i = 0; i < 16 && setup.FocusedLabel != until; i++) {
                if (shift) rig.ShiftKey(key);
                else rig.Key(key);
            }
            return setup.FocusedLabel;
        }
        Require(Walk(ImGuiKey.Tab, until: "Gratis glass") == "Gratis glass", "Tab walks to a look card");
        rig.Key(ImGuiKey.Space);
        Require(config.FlatCornerRadius == 48, "Space picks the focused card");
        var before = setup.FocusedLabel;
        rig.Key(ImGuiKey.RightArrow);
        var after = setup.FocusedLabel;
        rig.ShiftKey(ImGuiKey.Tab);
        Require(before != after && setup.FocusedLabel == before, "arrows and Shift+Tab move focus both ways");
        Require(Walk(ImGuiKey.LeftArrow, until: "Vanilla+") == "Vanilla+", "arrows reach the first card");
        rig.Key(ImGuiKey.Enter);
        Require(config.FlatCornerRadius == 12, "Enter on Vanilla+ restores the default look");
        Require(Walk(ImGuiKey.Tab, until: "Gratis icons") == "Gratis icons", "marker icon chips are reachable");
        rig.Key(ImGuiKey.Enter);
        Require(config.Markers.IconStyle == MarkerIconStyle.Icons, "Enter picks the icon style");
        ImGui.GetIO().BackendFlags |= ImGuiBackendFlags.HasGamepad;
        rig.Key(ImGuiKey.GamepadDpadLeft);
        rig.Key(ImGuiKey.GamepadFaceDown);
        Require(config.Markers.IconStyle == MarkerIconStyle.Game, "controller A presses the control the D-pad chose");
        var walked = Walk(ImGuiKey.GamepadDpadRight, until: "Next");
        Require(walked == "Next", "the D-pad moves focus, reached '" + walked + "'");
        rig.Key(ImGuiKey.GamepadFaceDown);
        Require(setup.Step == QuickSetupStep.Behaviours, "controller A presses Next");
        Require(Walk(ImGuiKey.Tab, until: QuickSetup.Behaviours[1].Label) == QuickSetup.Behaviours[1].Label,
            "toggles are reachable");
        rig.Key(ImGuiKey.Space);
        Require(config.QuestLogReplaceJournal, "Space flips the focused toggle");
        rig.Key(ImGuiKey.GamepadFaceRight);
        Require(!setup.IsOpen && config.QuickSetupDone && !config.QuestLogReplaceJournal,
            "controller B skips and puts the step back");
    }

    private static void Classic() {
        var config = Fresh();
        config.VisualStyle = 1;
        using var rig = new Rig(config);
        rig.Idle(4);
        Require(rig.Setup.IsOpen && rig.Setup.Visible && rig.Has("Next"), "the flow works in Classic");
        rig.Click("Next");
        rig.Click("Next");
        Require(rig.Setup.Step == QuickSetupStep.Done, "Classic skips the behaviours step");
        rig.Click("Finish");
        Require(!rig.Setup.IsOpen, "and finishes");
        var motion = Fresh();
        motion.ReduceMotion = false;
        using var animated = new Rig(motion);
        animated.Idle(1);
        Require(animated.Setup.Opacity < 1, "the panel fades in");
        animated.Idle(30);
        Require(animated.Setup.Opacity >= .999f, "and finishes fading");
        var reduced = Fresh();
        using var instant = new Rig(reduced);
        instant.Idle(2);
        Require(instant.Setup.Opacity >= .999f, "Reduce motion shows the panel at once");
        var small = Fresh(400, 300);
        using var narrow = new Rig(small, 400, 300);
        narrow.Idle(4);
        Require(narrow.Setup.IsOpen, "a small window still opens the flow");
    }

    private static void Cost() {
        var config = Fresh();
        config.QuickSetupDone = true;
        using var rig = new Rig(config);
        rig.Idle(4);
        long used = -1;
        rig.Driver.DrawPreviewOverlay = () => {
            var setup = rig.Setup;
            for (var i = 0; i < 200; i++) {
                setup.Prepare(rig.Driver);
                setup.Draw(rig.Driver);
            }
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2000; i++) {
                setup.Prepare(rig.Driver);
                setup.Draw(rig.Driver);
            }
            used = GC.GetAllocatedBytesForCurrentThread() - before;
        };
        rig.Idle(2);
        Require(used == 0, $"a closed flow allocates nothing per frame ({used} bytes over 2000 frames)");
    }
}
