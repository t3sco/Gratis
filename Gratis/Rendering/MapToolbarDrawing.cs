using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static partial class MapToolbarDrawing {
    public static Vector4 LastBounds { get; private set; }

    public static ChromeAnchor EffectiveAnchor { get; private set; }

    public static bool MapMenuOpen { get; private set; }

    public static Vector4 ClosingSearchBounds { get; private set; }

    public static bool SearchExitPointerOver => ClosingSearchBounds != Vector4.Zero
        && ImGui.IsMouseHoveringRect(new(ClosingSearchBounds.X, ClosingSearchBounds.Y),
            new(ClosingSearchBounds.Z, ClosingSearchBounds.W), false);

    private static bool compactDuringSearch;

    private static MapDrawContext? searchContext;

    public static Vector4 WindowBounds { get; private set; }

    public static Vector4[] ButtonBounds { get; } = new Vector4[FullButtonCount];

    public static Vector4[] GlyphBounds { get; } = new Vector4[FullButtonCount];

    public static Vector4[] DividerBounds { get; } = new Vector4[4];

    public const float DividerSpace = 8;

    public static float CollapsedWidth(int count) => count * GratisTheme.ControlHeight + 8
        + Math.Max(0, count - (count <= 3 ? 1 : 2)) * DividerSpace;

    public static void Draw(Action<ToolbarAction> activate, Action drawLayersContextMenu,
        PaletteOverlay? palette = null) {
        LastBounds = Vector4.Zero;
        ClosingSearchBounds = Vector4.Zero;
        Array.Clear(ButtonBounds);
        Array.Clear(GlyphBounds);
        Array.Clear(DividerBounds);
        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        WindowBounds = new(windowPos.X, windowPos.Y, windowPos.X + windowSize.X, windowPos.Y + windowSize.Y);
        MapMenuOpen = false;
        var context = MapDrawContext.Current;
        using var chromeAlpha = new ChromeVisibility.AlphaScope(context.Config.IsGratis,
            palette?.IsOpen == true ? 1 : context.Chrome.ControlsAlpha(context.Config));
        if (!chromeAlpha.Interactive)
            return;
        if (context.Config.IsGratis) {
            DrawSegmented(activate, drawLayersContextMenu, palette);
            return;
        }
        DrawClassic(context, activate, drawLayersContextMenu);
    }

    public const int FullButtonCount = 6;

    private const string MapMenu = "Gratis_Map_Controls";

    private static readonly string[] ButtonIds = [
        "##gratis-Search",
        "##gratis-Lens",
        "##gratis-Filters",
        "##gratis-Follow",
        "##gratis-Settings",
        "##gratis-Close"];

    private static readonly (string Id, ToolbarAction Action, string Tooltip)[] Buttons = [
        ("Search", ToolbarAction.Search, "Search places"), ("Lens", ToolbarAction.Layers, "Lenses & map layers"),
        ("Filters", ToolbarAction.CenterMap, "Filters"), ("Follow", ToolbarAction.Follow, "Follow player"),
        ("Settings", ToolbarAction.Settings, "Settings"), ("Close", ToolbarAction.Close, "Close map"),
    ];
}