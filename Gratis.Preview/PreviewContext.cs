using System.Numerics;
using Gratis.Classes;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal sealed class PreviewContext(Textures textures) : MapDrawContext
{
    private readonly Dictionary<uint, DrawTexture> icons = [];
    private readonly Dictionary<uint, IconSetting> settings = [];
    private readonly Dictionary<int, ImFontPtr> fonts = [];
    public override MapDrawingConfig Config { get; } = new() {
        HideWindowFrame = true, MapRounding = 24, MapBorder = true, MapGlow = true,
        MapVignette = .65f, MapGlowPulse = 0, ConeSize = 150,
    };
    public override float GlobalScale => 1;
    public override float MapSizeFactor => 1;

    public void BuildFonts(string path)
    {
        var atlas = ImGui.GetIO().Fonts;
        atlas.AddFontFromFileTTF(path, 18);
        foreach (var size in new[] { 14, 18 }) fonts[size] = atlas.AddFontFromFileTTF(path, size);
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
    public override bool PrepareMarker(MarkerInfo marker) => !GetIconSetting(marker.IconId).Hide;
    public override bool IsRadiusHovered(Vector2 center, float radius) => Vector2.Distance(ImGui.GetMousePos(), center) <= radius && ImGui.IsWindowHovered();
    public override IDisposable? PushLargeFont() => null;
    public override IDrawFont? GetTitleFont(int choice, float size) => choice <= 0 ? null : new Font(fonts.OrderBy(x => Math.Abs(x.Key - size)).First().Value);

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
        var clicked = ImGui.Button(Glyph(action), new Vector2(SettingsButtonWidth, ImGui.GetFrameHeight()));
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
        ImGui.PopID();
        return clicked;
    }
    public override float SettingsButtonWidth => 18 + ImGui.GetStyle().FramePadding.X * 2;
}
