using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

internal readonly record struct PopupProfile {
    public float Radius { get; init; }
    public float BorderSize { get; init; }
    public Vector4 Background { get; init; }
    public bool Controls { get; init; }
    public Vector2 Padding { get; init; }
    public float ChildRadius { get; init; }
    public float FrameRadius { get; init; }
    public float GrabInset { get; init; }
    public Vector2 FramePadding { get; init; }
    public Vector2 ItemSpacing { get; init; }
    public Vector4 Border { get; init; }
    public Vector4 Ink { get; init; }
    public Vector4 DisabledInk { get; init; }
    public Vector4 Fill { get; init; }
    public Vector4 HoverFill { get; init; }
    public Vector4 ActiveFill { get; init; }
}

internal static class PopupFaces {
    public static readonly PopupProfile Native = new() {
        Radius = GratisTheme.Radius.Popup, BorderSize = 0, Background = GratisTheme.Surface with { W = 1 },
    };
    public static readonly PopupProfile Chrome = Native with {
        Controls = true, Padding = new(12), ChildRadius = GratisTheme.Radius.Card,
        FrameRadius = GratisTheme.Radius.Control, GrabInset = 2,
        FramePadding = new(10, 7), ItemSpacing = new(8, 4), Border = GratisTheme.Border,
        Ink = GratisTheme.Primary, DisabledInk = GratisTheme.Secondary,
        Fill = new(1, 1, 1, .04f), HoverFill = new(1, 1, 1, .08f), ActiveFill = new(1, 1, 1, .12f),
    };
}

internal static class PopupSurface {
    public static Scope Push(in PopupProfile profile, float? scale = null, bool transparent = false) {
        var gratis = UiMotion.IsGratis;
        var s = profile.Controls && !gratis ? GratisTheme.ChromeScale : scale ?? GratisTheme.ChromeScale;
        var background = transparent ? Vector4.Zero : profile.Background;
        if (!profile.Controls) {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, profile.Radius * s);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, profile.Radius * s);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, profile.BorderSize);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, profile.BorderSize);
            ImGui.PushStyleColor(ImGuiCol.PopupBg, background);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, background);
            return new(4, 2);
        }
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, profile.Padding * s);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, profile.Radius * s);
        if (gratis) {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, profile.Radius * s);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, profile.BorderSize);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, profile.ChildRadius * s);
            ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, GratisTheme.Radius.Inner(profile.FrameRadius * s, profile.GrabInset * s));
            ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, GratisTheme.Radius.Pill(new(ImGui.GetStyle().ScrollbarSize)));
        }
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, profile.BorderSize);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, profile.FrameRadius * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, profile.FramePadding * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, profile.ItemSpacing * s);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, background);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, background);
        ImGui.PushStyleColor(ImGuiCol.Border, profile.Border);
        ImGui.PushStyleColor(ImGuiCol.Text, profile.Ink);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, profile.DisabledInk);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, profile.Fill);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, profile.HoverFill);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, profile.ActiveFill);
        ImGui.PushStyleColor(ImGuiCol.Header, profile.Fill);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, profile.HoverFill);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, profile.ActiveFill);
        ImGui.PushStyleColor(ImGuiCol.Button, profile.Fill);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, profile.HoverFill);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, profile.ActiveFill);
        return new(gratis ? 11 : 6, 14);
    }

    public readonly struct Scope(int variables, int colors) : IDisposable {
        public void Dispose() {
            ImGui.PopStyleColor(colors);
            ImGui.PopStyleVar(variables);
        }
    }
}
