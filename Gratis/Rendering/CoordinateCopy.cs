using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class CoordinateCopy {
    private static readonly HintRow[] ShiftHint = [new("Include map name", "Shift", true)];
    private static int inputFrame = -1, layoutFrame = -1;
    private static MapDrawContext? inputContext, layoutContext;
    public static Vector4 PlayerBounds { get; private set; }
    public static Vector4 CursorBounds { get; private set; }
    public static bool InputActive => inputFrame == ImGui.GetFrameCount()
        && ReferenceEquals(inputContext, MapDrawContext.Current);
    public static void RecordRows(Vector4 player, Vector4 cursor) {
        layoutFrame = ImGui.GetFrameCount();
        layoutContext = MapDrawContext.Current;
        PlayerBounds = player;
        CursorBounds = cursor;
    }

    public static void Record(Vector2 pos, Vector2 size, float playerWidth, float cursorWidth, float separator) {
        layoutFrame = ImGui.GetFrameCount();
        layoutContext = MapDrawContext.Current;
        var s = GratisTheme.ChromeScale;
        var x = pos.X + 12 * s;
        PlayerBounds = playerWidth > 0 ? new(x, pos.Y, x + playerWidth, pos.Y + size.Y) : Vector4.Zero;
        x += playerWidth + (playerWidth > 0 && cursorWidth > 0 ? separator : 0);
        CursorBounds = cursorWidth > 0 ? new(x, pos.Y, x + cursorWidth, pos.Y + size.Y) : Vector4.Zero;
    }
    public static void Draw(Vector2? player, Vector2? cursor) {
        var context = MapDrawContext.Current;
        if (!context.Config.IsGratis || context.ContextMenu.CapturesInput || context.QuickSetup.InputActive)
            return;
        if (layoutFrame != ImGui.GetFrameCount() || !ReferenceEquals(layoutContext, context))
            return;
        var saved = ImGui.GetCursorPos();
        void Value(string id, Vector2 position, Vector2 width, Vector2 value) {
            ImGui.SetCursorScreenPos(position);
            if (ImGui.InvisibleButton(id, width))
                context.ContextMenu.Copy(CoordinateShare.Text(value,
                    ImGui.GetIO().KeyShift ? context.ContextMenu.Actions?.MapName : null));
            if (ImGui.IsItemHovered() || ImGui.IsItemActive()) {
                inputFrame = ImGui.GetFrameCount();
                inputContext = context;
            }
            ChromeControls.Tooltip(id.Contains("cursor", StringComparison.Ordinal)
                ? "Copy cursor coordinates" : "Copy player coordinates", rows: ShiftHint);
        }
        void Target(string id, Vector4 bounds, Vector2? value) {
            if (value is not { } v || bounds == Vector4.Zero)
                return;
            Value(id, new(bounds.X, bounds.Y), new(bounds.Z - bounds.X, bounds.W - bounds.Y), v);
        }
        Target("##copy-player-coordinates", PlayerBounds, player);
        Target("##copy-cursor-coordinates", CursorBounds, cursor);
        ImGui.SetCursorPos(saved);
        ImGui.Dummy(Vector2.Zero);
    }
}