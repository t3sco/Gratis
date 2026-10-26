using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class UpdateNotice {
    private static volatile string available = "";
    public static string Available {
        get => available;
        set => available = value ?? "";
    }

    public static Action? OpenInstaller { get; set; }
    public static Vector4 LastBounds { get; private set; }
    public static Vector2 ActionCenter { get; private set; }
    public static Vector2 DismissCenter { get; private set; }

    public static void Draw() {
        LastBounds = Vector4.Zero;
        var context = MapDrawContext.Current;
        var config = context.Config;
        var version = Available;
        if (!config.IsGratis || !config.ShowUpdateNotice || version.Length == 0 || config.DismissedUpdate == version
            || context.ContextMenu.CapturesInput)
            return;
        using var alpha = new ChromeVisibility.AlphaScope(true, context.Chrome.ControlsAlpha(context.Config));
        if (!alpha.Visible)
            return;
        var s = GratisTheme.ChromeScale;
        using var font = context.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push();
        var label = $"Gratis {version} is out";
        var labelWidth = ImGui.CalcTextSize(label).X;
        var buttonSize = new Vector2(ImGui.CalcTextSize("Update").X + 24 * s, GratisTheme.ControlHeight * s - 8 * s);
        var closeSize = 28 * s;
        var size = new Vector2(12 * s + labelWidth + 12 * s + buttonSize.X + 4 * s + closeSize + 6 * s,
            GratisTheme.ControlHeight * s);
        var bounds = ChromeLayout.Place("update-notice", size, ChromeAnchor.TopRight);
        if (bounds == Vector4.Zero)
            return;
        LastBounds = bounds;
        var pos = ChromeLayout.Position(bounds);
        var list = ImGui.GetWindowDrawList();
        MapWindowDrawing.DrawSurface(list, pos, pos + size, GratisTheme.Radius.Card * s);
        list.AddText(pos + new Vector2(12 * s, (size.Y - ImGui.GetFontSize()) / 2),
            ImGui.GetColorU32(GratisTheme.Primary), label);
        var buttonPos = pos + new Vector2(12 * s + labelWidth + 12 * s, 4 * s);
        ImGui.SetCursorScreenPos(buttonPos);
        var pressed = ChromeControls.Button("##update-action", buttonSize);
        var hovered = ImGui.IsItemHovered();
        list.AddRectFilled(buttonPos, buttonPos + buttonSize,
            ImGui.GetColorU32(GratisTheme.Crystal with { W = hovered ? .30f : .20f }),
            GratisTheme.Radius.Inner(GratisTheme.Radius.Card * s, 4 * s));
        list.AddRect(buttonPos, buttonPos + buttonSize, ImGui.GetColorU32(GratisTheme.Crystal with { W = .45f }),
            GratisTheme.Radius.Inner(GratisTheme.Radius.Card * s, 4 * s), ImDrawFlags.None, s);
        list.AddText(buttonPos + new Vector2(12 * s, (buttonSize.Y - ImGui.GetFontSize()) / 2),
            ImGui.GetColorU32(GratisTheme.Primary), "Update");
        ChromeControls.Tooltip("Opens the plugin installer so you can update Gratis",
            windowBounds: MapToolbarDrawing.WindowBounds);
        ActionCenter = buttonPos + buttonSize / 2;
        if (pressed)
            OpenInstaller?.Invoke();
        var closePos = new Vector2(bounds.Z - 6 * s - closeSize, pos.Y + (size.Y - closeSize) / 2);
        DismissCenter = closePos + new Vector2(closeSize / 2);
        ImGui.SetCursorScreenPos(closePos);
        if (ChromeControls.IconButton("##update-dismiss", GratisIcons.X, "Hide until the next version",
                new Vector2(closeSize), windowBounds: MapToolbarDrawing.WindowBounds)) {
            config.DismissedUpdate = version;
            context.SaveConfig();
        }
    }
}
