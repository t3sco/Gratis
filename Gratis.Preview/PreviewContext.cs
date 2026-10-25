using System.Numerics;
using Gratis.Classes;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal readonly record struct BackdropBlur(nint List, int IndexOffset, Vector2 Min, Vector2 Max, float Rounding, float Radius, float Opacity);

internal sealed class PreviewContext(Textures textures, MapDrawingConfig? config = null) : MapDrawContext, IDisposable
{
    public List<BackdropBlur> Blurs { get; } = [];
    public bool BlurAvailable { get; set; } = true;
    public MapColourPreset ColourPreset { get; set; }
    public override MapColourPreset ActiveColourPreset => ColourPreset;
    public override unsafe bool TryBlurBehind(ImDrawListPtr list, Vector2 min, Vector2 max, float rounding, float radius, float opacity = 1, bool prepend = false)
    {
        if (!BlurAvailable || !float.IsFinite(radius) || radius <= 0) return false;
        Blur.Attempt(BlurDiagnostics.Strength(radius, Math.Clamp(opacity, 0, 1)));
        Blurs.Add(new BackdropBlur((nint)list.NativePtr, prepend ? 0 : list.IdxBuffer.Size, min, max, rounding, radius, Math.Clamp(opacity, 0, 1)));
        return true;
    }

    private readonly Dictionary<uint, DrawTexture> icons = [];
    private readonly Dictionary<uint, IconSetting> settings = [];
    private readonly Dictionary<int, ImFontPtr> fonts = [];
    private readonly Dictionary<(ChromeFontRole Role, int Size), ImFontPtr> chromeFonts = [];
    public override MapDrawingConfig Config { get; } = config ?? new() {
        HideWindowFrame = true, MapRounding = 24, MapBorder = true, MapGlow = true,
        MapVignette = .65f, MapGlowPulse = 0, ConeSize = 150,
    };
    public float DpiScale { get; set; } = 1;
    public override float GlobalScale => DpiScale;
    public long ClockUnixSeconds { get; set; } = 2544;
    public override long UnixTimeSeconds => ClockUnixSeconds;
    public float SizeFactor { get; set; } = 1;
    public override float MapSizeFactor => SizeFactor;

    private readonly Dictionary<int, ImFontPtr> iconFonts = [];
    private global::System.Runtime.InteropServices.GCHandle iconRanges;
    public void Dispose() { if (iconRanges.IsAllocated) iconRanges.Free(); }
    public override IDrawFont? GetIconFont(float size) => !Config.IsGratis || iconFonts.Count == 0 ? null :
        new Font(iconFonts.OrderBy(x => Math.Abs(x.Key - size)).First().Value);

    public unsafe void BuildFonts(string path)
    {
        var atlas = ImGui.GetIO().Fonts;
        var fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();
        fontConfig->OversampleH = 2;
        fontConfig->OversampleV = 1;
        fontConfig->PixelSnapH = 1;
        try {
            atlas.AddFontFromFileTTF(path, 18);
            foreach (var size in Config.IsGratis && Config.FrameFont != 0 ? TextDrawing.Sizes : new[] { 14, 18 })
                fonts[size] = atlas.AddFontFromFileTTF(path, size, Config.IsGratis ? new ImFontConfigPtr(fontConfig) : default);
            if (Config.IsGratis) {
                var iconPath = Path.Combine(Path.GetDirectoryName(path)!, "Fonts", "Lucide", "lucide.ttf");
                if (File.Exists(iconPath)) {
                    iconRanges = global::System.Runtime.InteropServices.GCHandle.Alloc(GratisIcons.GlyphRanges, global::System.Runtime.InteropServices.GCHandleType.Pinned);
                    foreach (var size in TextDrawing.Sizes)
                        iconFonts[size] = atlas.AddFontFromFileTTF(iconPath, size, new ImFontConfigPtr(fontConfig), iconRanges.AddrOfPinnedObject());
                }
            }
            foreach (var role in Enum.GetValues<ChromeFontRole>()) {
                var filename = role switch {
                    ChromeFontRole.Title => "Geist-SemiBold.ttf", ChromeFontRole.Chip => "Geist-Medium.ttf",
                    ChromeFontRole.Coordinates => "GeistMono-Regular.ttf", _ => "Geist-Regular.ttf",
                };
                foreach (var size in Config.IsGratis ? TextDrawing.Sizes : DpiScale == 1 ? new[] { 12, 14 } : new[] { 12, 14, 24, 28 })
                    chromeFonts[(role, size)] = atlas.AddFontFromFileTTF(Path.Combine(Path.GetDirectoryName(path)!, "Fonts", filename), size,
                        Config.IsGratis ? new ImFontConfigPtr(fontConfig) : default);
            }
        }
        finally { ImGuiNative.ImFontConfig_destroy(fontConfig); }
    }

    public override DrawTexture GetIcon(uint id)
    {
        if (icons.TryGetValue(id, out var icon)) return icon;
        const int size = 64;
        var rgba = new byte[size * size * 4];
        var color = id switch {
            60443 => new Vector3(245, 248, 255),
            60071 => new Vector3(245, 192, 71),
            60401 => new Vector3(114, 184, 246),
            _ => new Vector3(99 + id % 100, 160 + id % 70, 70 + id % 100),
        };
        for (var y = 0; y < size; y++) {
            for (var x = 0; x < size; x++) {
                var p = new Vector2(x + .5f, y + .5f) - new Vector2(size / 2f);
                var distance = id == 60443
                    ? MathF.Max(MathF.Abs(p.X) * .9f + p.Y * .45f - 5, -p.Y - 24)
                    : p.Length() - 25;
                var alpha = Math.Clamp(.5f - distance, 0, 1);
                var shade = id != 60443 && p.Length() > 20 ? .4f : 1;
                var i = (y * size + x) * 4;
                rgba[i] = (byte)(color.X * shade); rgba[i + 1] = (byte)(color.Y * shade);
                rgba[i + 2] = (byte)(color.Z * shade); rgba[i + 3] = (byte)(255 * alpha);
            }
        }
        icon = new DrawTexture(textures.Add(size, size, rgba), new Vector2(size));
        icons.Add(id, icon);
        return icon;
    }

    public override IconSetting GetIconSetting(uint id)
    {
        if (!settings.TryGetValue(id, out var setting)) settings[id] = setting = new IconSetting { IconId = id };
        return setting;
    }
    public override bool PrepareMarker(MarkerInfo marker) => !GetIconSetting(marker.IconId).Hide || Config.IsGratis && MarkerVisibility.IsPriority(marker);
    public override bool IsRadiusHovered(Vector2 center, float radius) => Vector2.Distance(ImGui.GetMousePos(), center) <= radius && ImGui.IsWindowHovered();
    public override IDisposable? PushLargeFont() => null;
    public override IDrawFont? GetTitleFont(int choice, float size) => choice <= 0 ? null : new Font(fonts.OrderBy(x => Math.Abs(x.Key - size)).First().Value);
    public override IDrawFont? GetChromeFont(float size, ChromeFontRole role) => !Config.IsGratis ? null :
        Config.FrameFont != 0 ? GetTitleFont(Config.FrameFont == 2 ? 0 : 1, size) :
        new Font(chromeFonts.Where(x => x.Key.Role == role).OrderBy(x => Math.Abs(x.Key.Size - size)).First().Value);

    private sealed class Font(ImFontPtr font) : IDrawFont
    {
        public IDisposable Push() { ImGui.PushFont(font); return new PopFont(); }
    }
    private sealed class PopFont : IDisposable { public void Dispose() => ImGui.PopFont(); }

    private static string Glyph(ToolbarAction action) => action switch {
        ToolbarAction.Parent => "^", ToolbarAction.Layers => "=", ToolbarAction.Follow => ">",
        ToolbarAction.CenterPlayer => "+", ToolbarAction.CenterMap => "#", ToolbarAction.Search => "?",
        ToolbarAction.Settings => "*", ToolbarAction.Close => "x", _ => "?",
    };
    public override bool IconButton(ToolbarAction action, string id, string tooltip)
    {
        ImGui.PushID(id);
        Trace?.Widget(ImGui.GetID(Glyph(action)));
        var clicked = ImGui.Button(Glyph(action), new Vector2(SettingsButtonWidth, ImGui.GetFrameHeight()));
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
        ImGui.PopID();
        return clicked;
    }
    public override float SettingsButtonWidth => 18 + ImGui.GetStyle().FramePadding.X * 2;
}
