using System.Numerics;
using Gratis.Classes;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal readonly record struct BackdropBlur(nint List, int IndexOffset, Vector2 Min, Vector2 Max, float Rounding,
    float Radius, float Opacity);

internal sealed class PreviewContext(Textures textures, MapDrawingConfig? config = null) : MapDrawContext, IDisposable {
    public List<BackdropBlur> Blurs { get; } = [];
    public bool PvP { get; set; }
    public override bool IsPvP => PvP;
    public ILocationMenuSource? LocationSource { get; set; }
    public override ILocationMenuSource? LocationMenu => LocationSource;
    public IQuestLogSource? QuestSource { get; set; }
    public override IQuestLogSource? QuestLogSource => QuestSource;
    private readonly Dictionary<uint, DrawTexture> crests = [];
    public override DrawTexture GetExpansionCrest(uint id) {
        if (crests.TryGetValue(id, out var crest))
            return crest;
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "crests", id + ".png");
        if (File.Exists(path)) {
            var handle = textures.Load(path);
            var texture = textures.Get(handle);
            crest = new(handle, new(texture.Width, texture.Height));
        }
        crests[id] = crest;
        return crest;
    }
    public bool BlurAvailable { get; set; } = true;
    public MapColourPreset ColourPreset { get; set; }
    public string? RequestedSettingsSection { get; private set; }
    public override void OpenFilterSettings() => RequestedSettingsSection = "Filters";
    public override MapColourPreset ActiveColourPreset => ColourPreset;
    public override unsafe bool TryBlurBehind(ImDrawListPtr list, Vector2 min, Vector2 max, float rounding,
        float radius, float opacity = 1, bool prepend = false) {
        if (!BlurAvailable || !float.IsFinite(radius) || radius <= 0)
            return false;
        Blur.Attempt(BlurDiagnostics.Strength(radius, Math.Clamp(opacity, 0, 1)));
        Blurs.Add(new BackdropBlur((nint)list.NativePtr, prepend ? 0 : list.IdxBuffer.Size, min, max, rounding, radius,
            Math.Clamp(opacity, 0, 1)));
        return true;
    }

    private readonly Dictionary<uint, DrawTexture> icons = [];
    private readonly Dictionary<int, ImFontPtr> fonts = [];
    private readonly Dictionary<(ChromeFontRole Role, int Size), ImFontPtr> chromeFonts = [];
    private readonly Dictionary<ImFontPtr, Font> textFontScopes = [];
    private ImFontPtr defaultTextFont;
    public FontAtlasBudget TextBudget { get; } = new(FontAtlasBudget.MaximumTextHandles,
        FontAtlasBudget.MaximumArea / 2);
    private global::System.Runtime.InteropServices.GCHandle textRanges;
    private global::System.Runtime.InteropServices.GCHandle fallbackRanges;
    public bool TextFontsReady { get; set; } = true;
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
    private readonly Dictionary<int, Font> iconDrawFonts = [];
    public bool IconFontsEnabled { get; set; } = true;
    public char MissingIconGlyph { get; set; }
    private global::System.Runtime.InteropServices.GCHandle iconRanges;
    public void Dispose() {
        if (iconRanges.IsAllocated)
            iconRanges.Free();
        if (textRanges.IsAllocated)
            textRanges.Free();
        if (fallbackRanges.IsAllocated)
            fallbackRanges.Free();
    }

    public override IDrawFont? GetIconFont(float size) {
        if (!Config.IsGratis || !IconFontsEnabled)
            return null;
        var pixels = TextDrawing.IconPixelSize(size);
        var nearest = -1;
        foreach (var entry in iconFonts)
            if (nearest < 0 || Math.Abs(entry.Key - pixels) < Math.Abs(nearest - pixels))
                nearest = entry.Key;
        return nearest >= 0 ? iconDrawFonts[nearest] : null;
    }

    public override bool TryIconGlyphBounds(char glyph, out Vector4 bounds) {
        if (glyph == MissingIconGlyph) {
            bounds = default;
            return false;
        }
        return base.TryIconGlyphBounds(glyph, out bounds);
    }

    public override string IconFontDiagnostics() =>
        $"icon-fonts enabled={IconFontsEnabled} built=[{string.Join(",", iconFonts.Keys)}]";

    public unsafe void BuildFonts(string path) {
        var atlas = ImGui.GetIO().Fonts;
        var fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();
        fontConfig->OversampleH = 1;
        fontConfig->OversampleV = 1;
        fontConfig->PixelSnapH = 1;
        try {
            defaultTextFont = atlas.AddFontFromFileTTF(path, 18);
            textFontScopes[defaultTextFont] = new Font(defaultTextFont);
            foreach (var size in Config.IsGratis ? Array.Empty<int>() : new[] { 14, 18 }) {
                fonts[size] = atlas.AddFontFromFileTTF(path, size,
                    Config.IsGratis ? new ImFontConfigPtr(fontConfig) : default);
                textFontScopes[fonts[size]] = new Font(fonts[size]);
            }
            if (Config.IsGratis) {
                var iconPath = Path.Combine(Path.GetDirectoryName(path)!, "Fonts", "Icons", "icons.ttf");
                if (File.Exists(iconPath)) {
                    iconRanges = global::System.Runtime.InteropServices.GCHandle.Alloc(GratisIcons.GlyphRanges,
                        global::System.Runtime.InteropServices.GCHandleType.Pinned);
                    foreach (var size in TextDrawing.IconSizes) {
                        iconFonts[size] = atlas.AddFontFromFileTTF(iconPath, size, new ImFontConfigPtr(fontConfig),
                            iconRanges.AddrOfPinnedObject());
                        iconDrawFonts[size] = new Font(iconFonts[size]);
                    }
                }
            }
            if (Config.IsGratis) {
                textRanges = global::System.Runtime.InteropServices.GCHandle.Alloc(FontAtlasBudget.TextRanges,
                    global::System.Runtime.InteropServices.GCHandleType.Pinned);
                fallbackRanges = global::System.Runtime.InteropServices.GCHandle.Alloc(FontAtlasBudget.FallbackRanges,
                    global::System.Runtime.InteropServices.GCHandleType.Pinned);
                foreach (var size in TextDrawing.Sizes) {
                    if (!TextBudget.Reserve(10, size, FontAtlasBudget.TextGlyphs))
                        throw new InvalidOperationException("Geist inventory exceeded its budget");
                    var font = atlas.AddFontFromFileTTF(Path.Combine(Path.GetDirectoryName(path)!, "Fonts",
                        "Geist-Regular.ttf"),
                        size, new ImFontConfigPtr(fontConfig), textRanges.AddrOfPinnedObject());
                    fontConfig->MergeMode = 1;
                    try {
                        atlas.AddFontFromFileTTF(Path.Combine(Path.GetDirectoryName(path)!,
                            "NotoSansCJK-Glyphs.otf"), size, new ImFontConfigPtr(fontConfig),
                            fallbackRanges.AddrOfPinnedObject());
                    }
                    finally { fontConfig->MergeMode = 0; }
                    textFontScopes[font] = new Font(font);
                    foreach (var role in Enum.GetValues<ChromeFontRole>())
                        chromeFonts[(role, size)] = font;
                }
            }
            else
                foreach (var role in Enum.GetValues<ChromeFontRole>()) {
                    var filename = role switch {
                        ChromeFontRole.Title => "Geist-SemiBold.ttf",
                        ChromeFontRole.Chip => "Geist-Medium.ttf",
                        ChromeFontRole.Coordinates => "GeistMono-Regular.ttf",
                        _ => "Geist-Regular.ttf",
                    };
                    foreach (var size in Config.IsGratis ? TextDrawing.Sizes : DpiScale == 1 ? new[] { 12, 14 }
                        : new[] { 12, 14, 24, 28 }) {
                        chromeFonts[(role, size)] = atlas.AddFontFromFileTTF(Path.Combine(Path.GetDirectoryName(path)!,
                            "Fonts", filename),
                            size, Config.IsGratis ? new ImFontConfigPtr(fontConfig) : default);
                        textFontScopes[chromeFonts[(role, size)]] = new Font(chromeFonts[(role, size)]);
                    }
                }
        }
        finally { ImGuiNative.ImFontConfig_destroy(fontConfig); }
    }

    public override DrawTexture GetIcon(uint id) {
        if (icons.TryGetValue(id, out var icon))
            return icon;
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
                var distance = id == 60443 ? MathF.Max(MathF.Abs(p.X) * .9f + p.Y * .45f - 5, -p.Y - 24)
                    : p.Length() - 25;
                var alpha = Math.Clamp(.5f - distance, 0, 1);
                var shade = id != 60443 && p.Length() > 20 ? .4f : 1;
                var i = (y * size + x) * 4;
                rgba[i] = (byte)(color.X * shade);
                rgba[i + 1] = (byte)(color.Y * shade);
                rgba[i + 2] = (byte)(color.Z * shade);
                rgba[i + 3] = (byte)(255 * alpha);
            }
        }
        icon = new DrawTexture(textures.Add(size, size, rgba), new Vector2(size));
        icons.Add(id, icon);
        return icon;
    }

    public override bool PrepareMarker(MarkerInfo marker) => true;
    public override bool IsRadiusHovered(Vector2 center, float radius) =>
        Vector2.Distance(ImGui.GetMousePos(), center) <= radius
        && ImGui.IsWindowHovered();
    public override IDisposable? PushLargeFont() => Config.IsGratis ? GetChromeFont(24, ChromeFontRole.Body)?.Push()
        : null;
    public override IDisposable PushDefaultTextFont() => textFontScopes[defaultTextFont].Push();
    public override IDrawFont? GetTitleFont(int choice, float size) {
        if (Config.IsGratis)
            return GetChromeFont(size, ChromeFontRole.Body);
        if (choice <= 0)
            return null;
        var nearest = -1;
        foreach (var entry in fonts)
            if (nearest < 0 || Math.Abs(entry.Key - size) < Math.Abs(nearest - size))
                nearest = entry.Key;
        return nearest >= 0 ? textFontScopes[fonts[nearest]] : textFontScopes[defaultTextFont];
    }

    public override IDrawFont? GetChromeFont(float size, ChromeFontRole role) {
        if (!Config.IsGratis)
            return null;
        if (!TextFontsReady)
            return textFontScopes[defaultTextFont];
        return chromeFonts.TryGetValue((role, TextDrawing.PixelSize(size)), out var font) ? textFontScopes[font]
            : textFontScopes[defaultTextFont];
    }

    public override string TextDiagnostics() {
        long iconArea = 0;
        foreach (var size in iconFonts.Keys)
            iconArea += FontAtlasBudget.Estimate(size, GratisIcons.GlyphRanges.Length / 2);
        return base.TextDiagnostics() + $" handles={TextBudget.Count}/{FontAtlasBudget.MaximumTextHandles} "
            + $"estimatedArea={TextBudget.EstimatedArea + iconArea}/{FontAtlasBudget.MaximumArea}px "
            + $"textArea={TextBudget.EstimatedArea} iconArea={iconArea} "
            + $"budgetWarning={TextBudget.Denied > 0 || TextBudget.EstimatedArea + iconArea > FontAtlasBudget.MaximumArea / 2} "
            + $"denied={TextBudget.Denied}";
    }

    private sealed class Font(ImFontPtr font) : IDrawFont {
        public IDisposable Push() {
            ImGui.PushFont(font);
            return new PopFont();
        }
    }
    private sealed class PopFont : IDisposable {
        public void Dispose() => ImGui.PopFont();
    }

    private static string Glyph(ToolbarAction action) => action switch {
        ToolbarAction.Parent => "^",
        ToolbarAction.Layers => "=",
        ToolbarAction.Follow => ">",
        ToolbarAction.CenterPlayer => "+",
        ToolbarAction.CenterMap => "#",
        ToolbarAction.Search => "?",
        ToolbarAction.Settings => "*",
        ToolbarAction.Close => "x",
        _ => "?",
    };
    public override bool IconButton(ToolbarAction action, string id, string tooltip) {
        ImGui.PushID(id);
        Trace?.Widget(ImGui.GetID(Glyph(action)));
        var clicked = ImGui.Button(Glyph(action), new Vector2(SettingsButtonWidth, ImGui.GetFrameHeight()));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
        ImGui.PopID();
        return clicked;
    }

    public override float SettingsButtonWidth => 18 + ImGui.GetStyle().FramePadding.X * 2;
}
