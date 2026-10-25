using System;
using System.Numerics;
using Gratis.Classes;
using Gratis.Data;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

#if GRATIS_PREVIEW
public readonly record struct DrawTexture(nint Handle, Vector2 Size);
#else
public readonly record struct DrawTexture(ImTextureID Handle, Vector2 Size);
#endif

public interface IDrawFont
{
    IDisposable Push();
}

public enum ToolbarAction { Parent, Layers, Follow, CenterPlayer, CenterMap, Search, Settings, Close }
public enum ChromeFontRole { Body, Chip, Title, Coordinates }

public abstract class MapDrawContext
{
    public static MapDrawContext Current { get; set; } =
#if GRATIS_PREVIEW
        null!;
#else
        new DalamudMapDrawContext();
#endif
    public MapFrameTrace? Trace { get; set; }
    public bool ReportCapture { get; set; }
    public MapAssistance? Assistance { get; set; }
    public EdgePointers EdgePointers { get; } = new();
    public MapMarkerTargets MarkerTargets { get; } = new();
    public MeasureTool Measure { get; } = new();
    public MainStoryObjective Story { get; } = new();
    public WindowPresetController WindowPresets { get; } = new();
    public ChromeVisibility Chrome { get; } = new();
    public BlurDiagnostics Blur { get; } = new();
    public int HoverCardFrame { get; set; } = -1;
    public MarkerBatch Markers { get; } = new();
    public MarkerRendering MarkerRendering { get; } = new();
    public MarkerVisibility Visibility { get; } = new();
    public virtual bool LensInDuty => false;
    public virtual bool LensGatheringClass => false;
    public virtual bool LensNearNode => false;
    public virtual void SaveConfig() { }
    public virtual void OpenFilterSettings() { }
    public virtual long UnixTimeSeconds => global::System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public FlagArrivalOverlay FlagArrival { get; } = new();
    public ContextMenu ContextMenu { get; } = new();
    public MapMotion Motion { get; } = new();
    public Spotlight? Spotlight { get; set; }
    public bool WindowMoveInputBlocked { get; set; }
    public virtual void ZoomCluster(Vector2 position, float scale) { }
    public abstract MapDrawingConfig Config { get; }
    public abstract float GlobalScale { get; }
    public abstract float MapSizeFactor { get; }
    public abstract DrawTexture GetIcon(uint id);
    public abstract IconSetting GetIconSetting(uint id);
    public abstract bool PrepareMarker(MarkerInfo marker);
    public abstract bool IsRadiusHovered(Vector2 center, float radius);
    public abstract IDisposable? PushLargeFont();
    public abstract IDrawFont? GetTitleFont(int choice, float size);
    public abstract IDrawFont? GetChromeFont(float size, ChromeFontRole role);
    public virtual IDisposable? PushDefaultTextFont() => null;
    public virtual IDrawFont? GetIconFont(float size) => null;
    public virtual bool TryIconGlyphBounds(char glyph, out Vector4 bounds) => DrawBinding.TryGlyphBounds(ImGui.GetFont(), glyph, out bounds);
    public virtual string IconFontDiagnostics() => "icon-fonts=host-unavailable";
    public virtual string TextDiagnostics() => $"text fontChoice={Config.FrameFont} uiScale={GlobalScale:0.###} " +
        $"nativePixels=true positionSnap=integer sizeLadder=[{string.Join(",", TextDrawing.Sizes)}] {TextDrawing.LastDraw}";
    public abstract bool IconButton(ToolbarAction action, string id, string tooltip);
    public abstract float SettingsButtonWidth { get; }
    public virtual MapColourPreset ActiveColourPreset => MapColourPreset.Original;
    public virtual bool TryBlurBehind(ImDrawListPtr list, Vector2 min, Vector2 max, float rounding, float radius, float opacity = 1, bool prepend = false) => false;
}
