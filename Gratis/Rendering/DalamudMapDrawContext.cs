using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Classes;
using Gratis.Classes;
using Gratis.Data;
using Math = global::System.Math;
using MathF = global::System.MathF;

namespace Gratis.Rendering;

internal sealed class DalamudMapDrawContext : MapDrawContext
{
    public override MapDrawingConfig Config => System.SystemConfig;
    public override float GlobalScale => ImGuiHelpers.GlobalScale;
    public override unsafe float MapSizeFactor => AgentMap.Instance()->SelectedMapSizeFactorFloat;
    public override DrawTexture GetIcon(uint id)
    {
        var texture = Service.TextureProvider.GetFromGameIcon(id).GetWrapOrEmpty();
        return new DrawTexture(texture.Handle, texture.Size);
    }
    public override IconSetting GetIconSetting(uint id) => System.IconConfig.IconSettingMap[id];
    public override bool PrepareMarker(MarkerInfo marker)
    {
        if (System.IconConfig.IconSettingMap.TryAdd(marker.IconId, new IconSetting { IconId = marker.IconId })) System.IconConfig.Save();
        if (GetIconSetting(marker.IconId).Hide) return false;
        if (marker.IsDynamicMarker) {
            foreach (var module in System.Modules) {
                if (module.ProcessMarker(marker)) break;
            }
        }
        return true;
    }
    public override bool IsRadiusHovered(Vector2 center, float radius) =>
        Vector2.Distance(ImGui.GetMousePos() - System.MapWindow.MapDrawOffset + ImGui.GetWindowPos(), center) <= radius && System.MapWindow.HoveredFlags.Any();
    public override IDisposable PushLargeFont() => System.LargeAxisFontHandle.Push();
    private sealed class Font(global::Dalamud.Interface.ManagedFontAtlas.IFontHandle handle) : IDrawFont
    {
        public IDisposable Push() => handle.Push();
    }
    public override IDrawFont? GetTitleFont(int choice, float size) => GetNativeTitleFont(choice, size) is { } font ? new Font(font) : null;
    private readonly global::System.Collections.Generic.Dictionary<(int Choice, int Size), global::Dalamud.Interface.ManagedFontAtlas.IFontHandle> titleFonts = [];

    private static readonly global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize[][] GameFontPresets = [
        [global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Axis96, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Axis12, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Axis14, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Axis18, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Axis36],
        [global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Meidinger16, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Meidinger20, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Meidinger40],
        [global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.TrumpGothic184, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.TrumpGothic23, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.TrumpGothic34, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.TrumpGothic68],
        [global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Jupiter16, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Jupiter20, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Jupiter23, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Jupiter45, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Jupiter90],
    ];

    private global::Dalamud.Interface.ManagedFontAtlas.IFontHandle? GetNativeTitleFont(int fontChoice, float sizePx)
    {
        if (fontChoice <= 0) return null;

        if (fontChoice == 1) {
            var size = Math.Max(8, (int)(sizePx + 0.5f));
            if (!titleFonts.TryGetValue((1, size), out var notoHandle)) {
                notoHandle = Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(toolkit => {
                    var config = new global::Dalamud.Interface.ManagedFontAtlas.SafeFontConfig { SizePx = size };
                    toolkit.AddDalamudAssetFont(global::Dalamud.DalamudAsset.NotoSansCjkMedium, config);
                }));
                titleFonts[(1, size)] = notoHandle;
            }

            return notoHandle;
        }

        var presets = GameFontPresets[fontChoice - 2];
        var best = presets[0];
        var bestDistance = float.MaxValue;
        foreach (var preset in presets) {
            var distance = MathF.Abs(new global::Dalamud.Interface.GameFonts.GameFontStyle(preset).SizePx - sizePx);
            if (distance < bestDistance) {
                bestDistance = distance;
                best = preset;
            }
        }

        var key = (fontChoice, (int)best);
        if (!titleFonts.TryGetValue(key, out var handle)) {
            handle = Service.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new global::Dalamud.Interface.GameFonts.GameFontStyle(best));
            titleFonts[key] = handle;
        }

        return handle;
    }


    private static FontAwesomeIcon Icon(ToolbarAction action) => action switch {
        ToolbarAction.Parent => FontAwesomeIcon.ArrowUp,
        ToolbarAction.Layers => FontAwesomeIcon.LayerGroup,
        ToolbarAction.Follow => FontAwesomeIcon.LocationArrow,
        ToolbarAction.CenterPlayer => FontAwesomeIcon.ArrowsToCircle,
        ToolbarAction.CenterMap => FontAwesomeIcon.MapMarked,
        ToolbarAction.Search => FontAwesomeIcon.Search,
        ToolbarAction.Settings => FontAwesomeIcon.Cog,
        ToolbarAction.Close => FontAwesomeIcon.Times,
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };
    public override bool IconButton(ToolbarAction action, string id, string tooltip) => GratisGuiTweaks.IconButton(Icon(action), id, tooltip);
    public override float SettingsButtonWidth {
        get {
            using var font = Service.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push();
            return ImGui.CalcTextSize(FontAwesomeIcon.Cog.ToIconString()).X + ImGui.GetStyle().FramePadding.X * 2.0f;
        }
    }
}
