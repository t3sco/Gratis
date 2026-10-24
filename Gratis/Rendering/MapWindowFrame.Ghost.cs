using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class MapWindowFrame
{
    public GhostMode Ghost { get; } = new();
    public const ImGuiWindowFlags GhostInputFlags = ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoNavInputs | ImGuiWindowFlags.NoNavFocus;
    private double hintUntil;

    private void PrepareGhost(IMapWindowHost host)
    {
        var config = host.Config;
        var context = MapDrawContext.Current;
        var hovered = ImGui.IsMouseHoveringRect(config.WindowPosition, config.WindowPosition + config.WindowSize, false);
        var blocked = host.SettingsOpen || isDragStarted || MoveHandle.Dragging || context.Measure.Dragging ||
            host.Palette?.IsOpen == true || host.Palette?.CapturesInput == true || context.Assistance?.InputActive == true || context.Assistance?.OverlayOpen == true ||
            context.ContextMenu.IsOpen || context.HoverCardFrame >= ImGui.GetFrameCount() - 1 ||
            ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);
        var interacting = hovered && (ImGui.IsAnyMouseDown() || ImGui.GetIO().MouseWheel != 0) || ImGui.IsAnyItemActive();
        Ghost.Update(config, host.Now, host.PlayerMoving, hovered, host.MapFocused, blocked, interacting);
        if (Ghost.ClickThrough) host.Flags |= GhostInputFlags;
        else host.Flags &= ~GhostInputFlags;
        if (config.IsGratis && config.GhostMode && !config.GhostHintSeen) {
            config.GhostHintSeen = true; hintUntil = host.Now + 5;
            context.SaveConfig();
        }
    }

    private void DrawGhostHint(IMapWindowHost host)
    {
        if (!host.Config.IsGratis || !host.Config.GhostMode || host.Now >= hintUntil) return;
        const string text = "Ghost mode: hover the map to restore input";
        var scale = MapDrawContext.Current.GlobalScale;
        using var font = MapDrawContext.Current.GetChromeFont(12 * scale, ChromeFontRole.Chip)?.Push();
        var size = ImGui.CalcTextSize(text) + new Vector2(16, 12) * scale;
        var pos = ImGui.GetWindowPos() + new Vector2((ImGui.GetWindowSize().X - size.X) / 2, 76 * scale);
        var list = ImGui.GetWindowDrawList();
        MapWindowDrawing.DrawSurface(list, pos, pos + size, 6 * scale);
        list.AddText(pos + new Vector2(8, 6) * scale, ImGui.GetColorU32(GratisTheme.Primary), text);
    }
}
