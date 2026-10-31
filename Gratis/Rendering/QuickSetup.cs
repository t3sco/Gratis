using System;
using System.Linq;
using System.Numerics;
using Gratis.Data;
using Gratis.Windows;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public enum QuickSetupStep { Welcome, Look, Behaviours, Done }

public readonly record struct QuickSetupBehaviour(string Path, string Id, string Label, string Detail);

/// <summary>
/// The first-install overlay on the map. Choices apply to the live settings as they are made; Undo and Skip put
/// the step back as it was when it opened. Closed, a frame costs a few field reads and allocates nothing.
/// </summary>
public sealed partial class QuickSetup {
    public const int MaxSlots = 16;
    public static readonly QuickSetupBehaviour[] Behaviours = [
        new("TravelActions", "##qs-travel", "Teleport actions",
            "Offer teleports for quests and places. Costly teleports still ask you to confirm."),
        new("QuestLogReplaceJournal", "##qs-journal", "Replace the game's Journal",
            "The Gratis Quest Log opens from the Journal key and /journal. Off unless you turn it on."),
        new("AutoHideChrome", "##qs-autohide", "Auto-hide controls",
            "Fade the toolbar and cards when idle. Hover to bring them back."),
        new("GhostMode", "##qs-ghost", "Click-through while moving",
            "Fade the map and let clicks reach the game while you move."),
    ];
    private static SettingsEntry[]? entries;
    private static SettingsEntry Entry(int index) => (entries ??= Array.ConvertAll(Behaviours,
        b => Array.Find(SettingsLayout.Entries, e => e.Path == b.Path)
            ?? throw new InvalidOperationException("Unknown setting " + b.Path)))[index];

    public bool IsOpen { get; private set; }
    public bool Visible { get; private set; }
    public bool InputActive { get; private set; }
    public QuickSetupStep Step { get; private set; }
    public Vector4 Bounds { get; private set; }
    // Only a panel that was really drawn takes the keyboard; a window too small for it leaves the keys to the game.
    public bool CapturesKeyboard => Visible && fits || swallowEscape;
    /// <summary>Brings the map window up; set by the window so Settings can start the flow on it.</summary>
    public Action? ShowMap { get; set; }

    /// <summary>What one step may change and its values when the step opened: only these are ever put back.</summary>
    private sealed class StepSnapshot(SettingsEntry[] entries, object[] values, bool hasIcon, MarkerIconStyle icon) {
        public bool Dirty;
        public void Restore(SystemConfig config) {
            for (var i = 0; i < entries.Length; i++)
                entries[i].Set(config, values[i]);
            if (hasIcon)
                config.Markers.IconStyle = icon;
            Dirty = false;
        }
    }

    private bool swallowEscape, gesture, keyboardFocus, fits, reveal;
    private int focus = -1, pending = -1, slotCount, primary;
    private float scroll;
    private readonly StepSnapshot?[] snapshots = new StepSnapshot?[4];
    private readonly Vector4[] hit = new Vector4[MaxSlots];
    private readonly string[] names = new string[MaxSlots];

    public int Slots => slotCount;
    public Vector4 BoundsOf(string label) {
        for (var i = 0; i < slotCount; i++)
            if (names[i] == label)
                return hit[i];
        return Vector4.Zero;
    }

    public float ScrollOffset => scroll;
    public bool HasChange => snapshots[(int)Step]?.Dirty == true;
    public string FocusedLabel => Focus() >= 0 && Focus() < slotCount ? names[Focus()] : "";
    public float Opacity { get; private set; }

    /// <summary>Opens the flow from the start, whatever the first-install flag says.</summary>
    public void Replay(SystemConfig config) {
        IsOpen = true;
        Step = QuickSetupStep.Welcome;
        Array.Clear(snapshots);
        fits = false;
        Enter(config);
        ShowMap?.Invoke();
    }

    private static StepSnapshot? Capture(SystemConfig config, QuickSetupStep step) {
        SettingsEntry[] entries = step switch {
            QuickSetupStep.Look => QuickSetupLooks.OwnedEntries,
            QuickSetupStep.Behaviours => Enumerable.Range(0, Behaviours.Length).Select(Entry).ToArray(),
            _ => [],
        };
        return entries.Length == 0 ? null : new(entries, Array.ConvertAll(entries, e => e.Value(config)),
            step == QuickSetupStep.Look, config.Markers.IconStyle);
    }

    /// <summary>Opens a step. A step left with unconfirmed changes keeps its original baseline, so it can still undo them.</summary>
    private void Enter(SystemConfig config) {
        focus = -1;
        keyboardFocus = reveal = false;
        scroll = 0;
        if (snapshots[(int)Step]?.Dirty != true)
            snapshots[(int)Step] = Capture(config, Step);
    }

    private void MarkDirty() {
        if (snapshots[(int)Step] is { } snapshot)
            snapshot.Dirty = true;
    }

    private void Close(SystemConfig config) {
        IsOpen = Visible = InputActive = gesture = false;
        Bounds = Vector4.Zero;
        Array.Clear(snapshots);
        // Any way out counts as done, so the flow never comes back by itself. The marker goes first and on its own,
        // so even a failed settings write cannot make a returning user look new.
        config.QuickSetupDone = true;
        MapDrawContext.Current.MarkQuickSetupDone();
        MapDrawContext.Current.SaveConfig();
    }

    /// <summary>Leaves now: every step with unconfirmed changes is put back, confirmed steps stay as chosen.</summary>
    public void Skip(SystemConfig config) {
        foreach (var snapshot in snapshots)
            if (snapshot is { Dirty: true })
                snapshot.Restore(config);
        Close(config);
    }

    public void Undo(SystemConfig config) {
        snapshots[(int)Step]?.Restore(config);
        MapDrawContext.Current.SaveConfig();
    }

    public void Next(SystemConfig config) {
        snapshots[(int)Step] = null;
        // Classic has no place search, teleport cards or quest log to configure.
        var skipsBehaviours = Step == QuickSetupStep.Look && !config.IsGratis;
        Step = skipsBehaviours ? QuickSetupStep.Done : Step + 1;
        if (skipsBehaviours) {
            // Behaviours left unconfirmed by an earlier Back are not kept just because the step is skipped.
            snapshots[(int)QuickSetupStep.Behaviours]?.Restore(config);
            snapshots[(int)QuickSetupStep.Behaviours] = null;
        }
        MapDrawContext.Current.SaveConfig();
        Enter(config);
    }

    public void Back(SystemConfig config) {
        Step = Step == QuickSetupStep.Done && !config.IsGratis ? QuickSetupStep.Look : Step - 1;
        Enter(config);
    }

    public void Finish(SystemConfig config) => Close(config);

    public void ChooseLook(SystemConfig config, int look) {
        if (QuickSetupLooks.Apply(config, look) > 0)
            MarkDirty();
    }

    public void ChooseIcons(SystemConfig config, MarkerIconStyle style) {
        if (config.Markers.IconStyle == style)
            return;
        config.Markers.IconStyle = style;
        MarkDirty();
    }

    public void Toggle(SystemConfig config, int behaviour, bool value) {
        Entry(behaviour).Set(config, value);
        MarkDirty();
    }

    public bool BehaviourValue(SystemConfig config, int behaviour) => (bool)Entry(behaviour).Value(config);

    private static bool Pressed(ImGuiKey key, bool repeat = true) => ImGui.IsKeyPressed(key, repeat);

    private int Focus() => focus >= 0 ? focus : primary;

    /// <summary>Decides whether the flow shows this frame and reads its keys. Cheap and allocation-free when closed.</summary>
    public void Prepare(IMapWindowHost host) {
        var context = MapDrawContext.Current;
        var config = host.Config;
        swallowEscape &= ImGui.IsKeyDown(ImGuiKey.Escape);
        Visible = InputActive = false;
        pending = -1;
        if (!IsOpen) {
            // A fresh install only: after the spoiler warning, on an open map, outside PvP, loading and duties.
            if (config.QuickSetupDone || !config.AcceptedSpoilerWarning || context.IsPvP || context.QuickSetupBlocked)
                return;
            IsOpen = true;
            Step = QuickSetupStep.Welcome;
            Enter(config);
        }
        if (context.IsPvP || context.QuickSetupBlocked || host.Palette?.IsOpen == true) {
            gesture = fits = false;
            return;
        }
        Visible = true;
        var over = Bounds.Z > Bounds.X && ImGui.IsMouseHoveringRect(new(Bounds.X, Bounds.Y), new(Bounds.Z, Bounds.W),
            false);
        gesture = ImGui.IsAnyMouseDown() && (gesture || over);
        InputActive = over || gesture;
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            keyboardFocus = false;
        if (over && fits && ImGui.GetIO().MouseWheel != 0) {
            scroll -= ImGui.GetIO().MouseWheel * WheelStep * GratisTheme.ChromeScale;
            reveal = false;
        }
        // A panel the window has no room to draw takes no keys and no gamepad input.
        if (!host.MapFocused || !fits)
            return;
        ImGui.SetNextFrameWantCaptureKeyboard(true);
        if (Pressed(ImGuiKey.Escape, false) || Pressed(ImGuiKey.GamepadFaceRight, false)) {
            swallowEscape = true;
            Skip(config);
            return;
        }
        var shift = ImGui.GetIO().KeyShift;
        var forward = Pressed(ImGuiKey.RightArrow) || Pressed(ImGuiKey.DownArrow) || Pressed(ImGuiKey.GamepadDpadRight)
            || Pressed(ImGuiKey.GamepadDpadDown) || Pressed(ImGuiKey.Tab) && !shift;
        var backward = Pressed(ImGuiKey.LeftArrow) || Pressed(ImGuiKey.UpArrow) || Pressed(ImGuiKey.GamepadDpadLeft)
            || Pressed(ImGuiKey.GamepadDpadUp) || Pressed(ImGuiKey.Tab) && shift;
        if ((forward || backward) && slotCount > 0) {
            focus = (Focus() + (forward ? 1 : slotCount - 1)) % slotCount;
            keyboardFocus = reveal = true;
        }
        if (Pressed(ImGuiKey.Enter, false) || Pressed(ImGuiKey.KeypadEnter, false) || Pressed(ImGuiKey.Space, false)
            || Pressed(ImGuiKey.GamepadFaceDown, false)) {
            pending = Focus();
            keyboardFocus = true;
        }
    }
}
