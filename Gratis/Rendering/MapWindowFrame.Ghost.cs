using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class MapWindowFrame {
    public GhostMode Ghost { get; } = new();
    public const ImGuiWindowFlags GhostInputFlags = ImGuiWindowFlags.NoMouseInputs
        | ImGuiWindowFlags.NoNavInputs
        | ImGuiWindowFlags.NoNavFocus;
    private double hintUntil;

    private void PrepareGhost(IMapWindowHost host) {
        var config = host.Config;
        var context = MapDrawContext.Current;
        var hovered = ImGui.IsMouseHoveringRect(config.WindowPosition, config.WindowPosition + config.WindowSize,
            false);
        var blocked = host.SettingsOpen || isDragStarted || MoveHandle.Dragging || context.Measure.Dragging
            || host.Palette?.IsOpen == true || host.Palette?.CapturesInput == true
            || context.Assistance?.InputActive == true || context.Assistance?.OverlayOpen == true
            || context.ContextMenu.IsOpen || context.HoverCardFrame >= ImGui.GetFrameCount() - 1
            || ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);
        var interacting = hovered && (ImGui.IsAnyMouseDown() || ImGui.GetIO().MouseWheel != 0)
            || ImGui.IsAnyItemActive();
        Ghost.Update(config, host.Now, host.PlayerMoving, hovered, host.MapFocused, blocked, interacting);
        if (Ghost.ClickThrough)
            host.Flags |= GhostInputFlags;
        else
            host.Flags &= ~GhostInputFlags;
        if (config.IsGratis && config.GhostMode && !config.GhostHintSeen) {
            config.GhostHintSeen = true;
            hintUntil = host.Now + 5;
            context.SaveConfig();
        }
    }

    private void DrawGhostHint(IMapWindowHost host) {
        if (!host.Config.IsGratis)
            return;
        var alpha = UiMotion.Visibility(ImGui.GetID("##ghost-coach"), host.Config.GhostMode && host.Now < hintUntil);
        if (alpha <= .001f)
            return;
        const string text = "Ghost mode: hover the map to restore input";
        var scale = GratisTheme.ChromeScale;
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * scale,
            ChromeFontRole.Chip)?.Push();
        var size = ImGui.CalcTextSize(text) + new Vector2(24, 16) * scale;
        var bounds = ChromeLayout.Place("ghost-hint", size, ChromeAnchor.TopCenter);
        if (bounds == Vector4.Zero)
            return;
        var pos = ChromeLayout.Position(bounds);
        var list = ImGui.GetWindowDrawList();
        using var motion = new UiMotion.VisualScope(list, pos, alpha,
            UiMotion.Full ? new Vector2(0, (1 - alpha) * 6 * scale) : Vector2.Zero);
        MapWindowDrawing.DrawSurface(list, pos, pos + size, GratisTheme.CardRadius * scale);
        list.AddText(pos + new Vector2(12, 8) * scale, ImGui.GetColorU32(GratisTheme.Primary), text);
    }
}