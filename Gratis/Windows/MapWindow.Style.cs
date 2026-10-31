using System;
﻿using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Classes;
using Gratis.Data;

namespace Gratis.Windows;

public partial class MapWindow {
    private int pushedGripColors;

    private bool pushedChromeStyles;

    public override void PreDraw() {
        windowFrame.PreDraw(this);
        Rendering.MapDrawContext.Current.Blur.BeginFrame(ImGui.GetFrameCount());
        if (System.SystemConfig.LockWindow || System.SystemConfig.IsGratis || System.SystemConfig.ClassicMoveHandle)
            Flags |= ImGuiWindowFlags.NoMove;
        pushedGripColors = 0;
        pushedChromeStyles = System.SystemConfig.IsGratis && System.SystemConfig.NeutralFrame;
        if (pushedChromeStyles) {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
            // ImGui sizes the corner resize zones from the window rounding. The frame is drawn by Gratis, so the
            // rounding is only a hit-size knob here: raise it so the corners are easier to grab.
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,
                MathF.Max(System.SystemConfig.FlatCornerRadius, 26) * ImGuiHelpers.GlobalScale);
        }
        if ((IsFocused || HoveredFlags.Any()) && !pushedChromeStyles)
            return;

        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, Vector4.Zero);
        pushedGripColors = 3;
    }

    public override void PostDraw() {
        if (pushedGripColors > 0)
            ImGui.PopStyleColor(pushedGripColors);
        if (pushedChromeStyles)
            ImGui.PopStyleVar(3);
        pushedChromeStyles = false;
        pushedGripColors = 0;
    }

    private unsafe void UpdateStyle() {
        if (System.SystemConfig.UseCustomWindowChrome) {
            Flags |= ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground;
        }
        else {
            Flags &= ~(ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground);
        }

        if (System.SystemConfig.LockWindow) {
            Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;
        }
        else {
            Flags &= ~(ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);
        }

        if (System.SystemConfig.NoFocusOnAppear) {
            Flags |= ImGuiWindowFlags.NoFocusOnAppearing;
        }
        else {
            Flags &= ~ImGuiWindowFlags.NoFocusOnAppearing;
        }

        if (System.SystemConfig.HideWindowBackground
            || (System.SystemConfig.IsGratis && System.SystemConfig.NeutralFrame)) {
            Flags |= ImGuiWindowFlags.NoBackground;
        }
        else {
            Flags &= ~ImGuiWindowFlags.NoBackground;
        }

        if (!PlacePalette.Overlay.IsOpen && !PlacePalette.Overlay.CapturesInput
            && !Rendering.LocationMenuDrawing.CapturesKeyboard
            && !Rendering.MapDrawContext.Current.QuestLog.CapturesKeyboard
            && !Rendering.MapDrawContext.Current.QuickSetup.CapturesKeyboard
            && Rendering.MapDrawContext.Current.Assistance?.PinLayer?.CapturesInput != true
            && !Rendering.MapDrawContext.Current.ContextMenu.CapturesInput && Service.KeyState[VirtualKey.ESCAPE]
            && IsFocused && !IsMapLocked()) {
            AgentMap.Instance()->Hide();
        }

    }

    private void UpdateSizePosition() {
        var systemConfig = System.SystemConfig;
        var windowPosition = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();

        if (!IsFocused) {
            if (windowPosition != systemConfig.WindowPosition) {
                ImGui.SetWindowPos(systemConfig.WindowPosition);
            }

            if (windowSize != systemConfig.WindowSize) {
                ImGui.SetWindowSize(systemConfig.WindowSize);
            }
        }
        else {
            if (systemConfig.WindowPosition != windowPosition) {
                systemConfig.WindowPosition = windowPosition;
                SystemConfig.Save();
            }

            if (systemConfig.WindowSize != windowSize) {
                systemConfig.WindowSize = windowSize;
                SystemConfig.Save();
            }
        }
    }
}