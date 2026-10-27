using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public enum FieldClear { None, Adjacent, Overlay }

public readonly record struct FieldProfile {
    public string Hint { get; init; }
    public int Capacity { get; init; }
    public ImGuiInputTextFlags Flags { get; init; }
    public Vector2? Padding { get; init; }
    public bool CenterPadding { get; init; }
    public float? Radius { get; init; }
    public Vector4? Fill { get; init; }
    public Vector4? Ink { get; init; }
    public Vector2 IconPosition { get; init; }
    public float IconSize { get; init; }
    public FieldClear Clear { get; init; }
    public float ClearSize { get; init; }
    public float ClearWidthDivisor { get; init; }
}

public static class FieldFaces {
    public static readonly FieldProfile Palette = new() {
        Hint = "Search places...", Capacity = 256, Padding = new(8, 0), CenterPadding = true,
        Radius = GratisTheme.Radius.Inner(GratisTheme.Radius.Card, 4),
        Fill = new(1, 1, 1, .055f), Ink = GratisTheme.Primary,
        Clear = FieldClear.Adjacent, ClearWidthDivisor = 3,
    };
    public static readonly FieldProfile Quest = new() { Hint = "Search quests...", Capacity = 256 };
    public static readonly FieldProfile Location = new() {
        Hint = "Find a zone", Capacity = 256, Flags = ImGuiInputTextFlags.EnterReturnsTrue,
    };
    public static readonly FieldProfile Settings = new() {
        Hint = "Search settings...", Capacity = 128, Padding = new(32, 11),
        IconPosition = new(16, 18), IconSize = 14, Clear = FieldClear.Overlay, ClearSize = 36,
    };
    public static readonly FieldProfile ClassicSettings = new() { Hint = "Search settings", Capacity = 128 };
}

public readonly record struct FieldOptions(FieldProfile Profile, Vector2 Size = default, float Scale = 1);
public readonly record struct FieldResult(bool Changed, bool Cleared, Vector4 ClearBounds);

public static class TextField {
#if GRATIS_PREVIEW
    internal static Action<string>? PreviewAfterInput;
#endif

    public static FieldResult Draw(string id, ref string text, in FieldOptions options) {
        var face = options.Profile;
        var s = options.Scale;
        var start = ImGui.GetCursorScreenPos();
        var width = options.Size == default ? ImGui.GetContentRegionAvail().X : options.Size.X;
        var clearSize = face.Clear == FieldClear.Adjacent
            ? MathF.Min(options.Size.Y, width / face.ClearWidthDivisor) : face.ClearSize * s;
        var vars = 0;
        var colors = 0;
        var radius = face.Radius * s;
        if (face.Padding is { } padding) {
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, face.CenterPadding
                ? new Vector2(padding.X * s, MathF.Max(0, (options.Size.Y - ImGui.GetFontSize()) / 2))
                : padding * s);
            vars++;
        }
        if (radius is { } rounding) {
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, rounding);
            vars++;
        }
        if (face.Fill is { } fill) { ImGui.PushStyleColor(ImGuiCol.FrameBg, fill); colors++; }
        if (face.Ink is { } ink) { ImGui.PushStyleColor(ImGuiCol.Text, ink); colors++; }
        bool changed;
        try {
            ImGui.SetNextItemWidth(face.Clear == FieldClear.Adjacent ? MathF.Max(1, width - clearSize) : -1);
#if GRATIS_PREVIEW
            changed = ImGui.InputTextWithHint(id, face.Hint, ref text, (uint)face.Capacity, face.Flags);
            PreviewAfterInput?.Invoke(id);
#else
            changed = ImGui.InputTextWithHint(id, face.Hint, ref text, face.Capacity, face.Flags);
#endif
        }
        finally {
            ImGui.PopStyleColor(colors);
            ImGui.PopStyleVar(vars);
        }
        if (face.IconSize > 0)
            GratisIcons.Draw(ImGui.GetWindowDrawList(), start + face.IconPosition * s,
                GratisIcons.Search, GratisTheme.Secondary, face.IconSize * s);
        var cleared = false;
        var clearBounds = Vector4.Zero;
        if (face.Clear == FieldClear.Adjacent || face.Clear == FieldClear.Overlay && text.Length > 0) {
            var next = ImGui.GetCursorPos();
            var clearPos = start + new Vector2(width - clearSize, 0);
            var size = new Vector2(clearSize, face.Clear == FieldClear.Adjacent ? options.Size.Y : clearSize);
            ImGui.SetCursorScreenPos(clearPos);
            clearBounds = new(clearPos.X, clearPos.Y, clearPos.X + size.X, clearPos.Y + size.Y);
            cleared = face.Clear == FieldClear.Adjacent
                ? ChromeControls.IconButton("##clear-search", GratisIcons.X, "Clear search", size,
                    windowBounds: MapToolbarDrawing.WindowBounds, radius: radius)
                : ChromeControls.IconButton("##clear-search", GratisIcons.X, "Clear search", size, s);
            if (face.Clear == FieldClear.Overlay) {
                ImGui.SetCursorPos(next);
                ImGui.Dummy(Vector2.Zero);
            }
        }
        return new(changed, cleared, clearBounds);
    }
}
