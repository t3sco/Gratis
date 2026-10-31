using System;
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
    public bool CapturesKeyboard => Visible || swallowEscape;
    /// <summary>Brings the map window up; set by the window so Settings can start the flow on it.</summary>
    public Action? ShowMap { get; set; }

    private bool swallowEscape, gesture, keyboardFocus, dirty;
    private int focus = -1, pending = -1, slotCount, primary;
    private string snapshot = "";
    private MarkerIconStyle iconBefore;
    private readonly Vector4[] hit = new Vector4[MaxSlots];
    private readonly string[] names = new string[MaxSlots];

    public int Slots => slotCount;
    public Vector4 BoundsOf(string label) {
        for (var i = 0; i < slotCount; i++)
            if (names[i] == label)
                return hit[i];
        return Vector4.Zero;
    }

    public bool HasChange => dirty;
    public string FocusedLabel => Focus() >= 0 && Focus() < slotCount ? names[Focus()] : "";
    public float Opacity { get; private set; }

    /// <summary>Opens the flow from the start, whatever the first-install flag says.</summary>
    public void Replay(SystemConfig config) {
        IsOpen = true;
        Step = QuickSetupStep.Welcome;
        Enter(config);
        ShowMap?.Invoke();
    }

    private void Enter(SystemConfig config) {
        snapshot = ProfileCodec.Encode("Before", ProfileCodec.Capture(config));
        iconBefore = config.Markers.IconStyle;
        dirty = false;
        focus = -1;
        keyboardFocus = false;
    }

    private void Restore(SystemConfig config) {
        if (ProfileCodec.TryDecode(snapshot, out var data, out _))
            ProfileCodec.Apply(config, data!);
        config.Markers.IconStyle = iconBefore;
        dirty = false;
    }

    private void Close(SystemConfig config) {
        IsOpen = Visible = InputActive = gesture = false;
        Bounds = Vector4.Zero;
        // Any way out counts as done, so the flow never comes back by itself.
        config.QuickSetupDone = true;
        MapDrawContext.Current.SaveConfig();
    }

    /// <summary>Leaves now: the step in progress is put back, earlier steps stay as chosen.</summary>
    public void Skip(SystemConfig config) {
        if (dirty)
            Restore(config);
        Close(config);
    }

    public void Undo(SystemConfig config) {
        Restore(config);
        MapDrawContext.Current.SaveConfig();
    }

    public void Next(SystemConfig config) {
        // Classic has no place search, teleport cards or quest log to configure.
        Step = Step == QuickSetupStep.Look && !config.IsGratis ? QuickSetupStep.Done : Step + 1;
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
            dirty = true;
    }

    public void ChooseIcons(SystemConfig config, MarkerIconStyle style) {
        if (config.Markers.IconStyle == style)
            return;
        config.Markers.IconStyle = style;
        dirty = true;
    }

    public void Toggle(SystemConfig config, int behaviour, bool value) {
        Entry(behaviour).Set(config, value);
        dirty = true;
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
            gesture = false;
            return;
        }
        Visible = true;
        var over = Bounds.Z > Bounds.X && ImGui.IsMouseHoveringRect(new(Bounds.X, Bounds.Y), new(Bounds.Z, Bounds.W),
            false);
        gesture = ImGui.IsAnyMouseDown() && (gesture || over);
        InputActive = over || gesture;
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            keyboardFocus = false;
        if (!host.MapFocused)
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
            keyboardFocus = true;
        }
        if (Pressed(ImGuiKey.Enter, false) || Pressed(ImGuiKey.KeypadEnter, false) || Pressed(ImGuiKey.Space, false)
            || Pressed(ImGuiKey.GamepadFaceDown, false)) {
            pending = Focus();
            keyboardFocus = true;
        }
    }
}
