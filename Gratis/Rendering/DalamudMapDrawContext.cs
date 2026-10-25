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

internal sealed class DalamudMapDrawContext : MapDrawContext, IDisposable
{
    private bool loggedBlurSuccess;
    private bool loggedBlurFailure;

    public override void OpenFilterSettings() => System.ConfigWindow.OpenSection("Filters");
    public override bool TryBlurBehind(ImDrawListPtr list, Vector2 min, Vector2 max, float rounding, float radius, float opacity = 1, bool prepend = false)
    {
        var frame = ImGui.GetFrameCount();
        if (!Blur.CanAttempt(frame) || !float.IsFinite(radius) || radius <= 0 || !float.IsFinite(opacity) || opacity < 1 / 255f) return false;
        var strength = BlurDiagnostics.Strength(radius, Math.Clamp(opacity, 0, 1));
        Blur.Attempt(strength);
        try {
            AddHostBlur(list, min, max, rounding, strength, prepend);
            Blur.Succeeded();
            if (!loggedBlurSuccess) {
                loggedBlurSuccess = true;
                Service.Log.Information($"[Gratis] First blur callback recorded (strength {strength:0.##}); /gratis glass reports recording status, not GPU completion.");
            }
            return true;
        }
        catch (Exception error) {
            Blur.Failed(error, frame);
            if (!loggedBlurFailure) {
                loggedBlurFailure = true;
                Service.Log.Information($"[Gratis] First blur recording failure; using tinted glass and retrying after 120 frames: {Blur.LastException}");
            }
            return false;
        }
    }

    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void AddHostBlur(ImDrawListPtr list, Vector2 min, Vector2 max, float rounding, float strength, bool prepend)
    {
        if (prepend) ImGuiHelpers.PrependBlurBehind(list, min, max, strength, rounding, Vector4.Zero, Vector4.Zero, 0);
        else ImGuiHelpers.AddBlurBehind(list, min, max, strength, rounding, Vector4.Zero, Vector4.Zero, 0);
    }

    public override MapDrawingConfig Config => System.SystemConfig;
    public override void SaveConfig() => Data.SystemConfig.Save();
    public override bool LensInDuty => Service.Condition[global::Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty];
    public override bool LensGatheringClass => Service.ObjectTable.LocalPlayer?.ClassJob.RowId is 16 or 17 or 18;
    public override bool LensNearNode {
        get {
            if (Service.ObjectTable.LocalPlayer is not { } player) return false;
            foreach (var obj in Service.ObjectTable)
                if (obj.ObjectKind == global::Dalamud.Game.ClientState.Objects.Enums.ObjectKind.GatheringPoint && Vector3.DistanceSquared(player.Position, obj.Position) < 900) return true;
            return false;
        }
    }
    public override MapColourPreset ActiveColourPreset => System.SystemConfig.MapColour?.Preset ?? MapColourPreset.Original;
    public override void ZoomCluster(Vector2 position, float scale) => System.MapRenderer.ZoomToCluster(position, scale);
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
        if (Config.IsGratis && Config.AetheryteAttunementStyle)
            marker.AetheryteAttuned = System.MapWindow.PlacePalette.MarkerAttunement(marker.DataId ?? 0, MarkerIcon.Family(marker));
        if (System.IconConfig.IconSettingMap.TryAdd(marker.IconId, new IconSetting { IconId = marker.IconId })) System.IconConfig.Save();
        if (GetIconSetting(marker.IconId).Hide && (!Config.IsGratis || !MarkerVisibility.IsPriority(marker))) return false;
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
    public override IDrawFont? GetTitleFont(int choice, float size) => GetNativeTitleFont(choice, size) is { Available: true } font ? new Font(font) : null;
    private readonly global::System.Collections.Generic.Dictionary<(int Choice, int Size), global::Dalamud.Interface.ManagedFontAtlas.IFontHandle> titleFonts = [];
    private bool fontFailureLogged;

    public override IDrawFont? GetChromeFont(float size, ChromeFontRole role)
    {
        try { return GetChromeFontCore(size, role); }
        catch (Exception error) {
            if (!fontFailureLogged) {
                fontFailureLogged = true;
                Service.Log.Warning(error, "Font atlas unavailable; retaining the current native font.");
            }
            return null;
        }
    }

    private IDrawFont? GetChromeFontCore(float size, ChromeFontRole role)
    {
        if (!Config.IsGratis) return null;
        if (Config.FrameFont == 2) return GetTitleFont(2, size);
        var fallback = GetNativeTitleFont(1, size)!;
        if (Config.FrameFont != 0) return fallback.Available ? new Font(fallback) : null;

        var pixels = TextDrawing.PixelSize(size);
        var key = (10 + (int)role, pixels);
        if (!titleFonts.TryGetValue(key, out var handle)) {
            var filename = role switch {
                ChromeFontRole.Title => "Geist-SemiBold.ttf",
                ChromeFontRole.Chip => "Geist-Medium.ttf",
                ChromeFontRole.Coordinates => "GeistMono-Regular.ttf",
                _ => "Geist-Regular.ttf",
            };
            handle = Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(toolkit => {
                var config = new SafeFontConfig { SizePx = pixels, OversampleH = 2, OversampleV = 1, PixelSnapH = true };
                try {
                    using var stream = typeof(DalamudMapDrawContext).Assembly.GetManifestResourceStream($"Gratis.Assets.Fonts.{filename}")
                        ?? throw new global::System.IO.FileNotFoundException(filename);
                    var geist = toolkit.AddFontFromStream(stream, config, false, filename);
                    config.MergeFont = geist;
                }
                catch (Exception error) {
                    Service.Log.Warning(error, "Geist font unavailable; using Noto.");
                }
                toolkit.AddDalamudAssetFont(global::Dalamud.DalamudAsset.NotoSansCjkMedium, config);
            }));
            titleFonts[key] = handle;
        }
        return handle.Available ? new Font(handle) : fallback.Available ? new Font(fallback) : null;
    }

    private readonly global::System.Collections.Generic.Dictionary<int, IFontHandle> iconFonts = [];
    private readonly global::System.Collections.Generic.Dictionary<int, Font> iconDrawFonts = [];
    private bool iconFontFailed;

    public override IDrawFont? GetIconFont(float size)
    {
        if (iconFontFailed) return null;
        var pixels = TextDrawing.PixelSize(size);
        if (!iconFonts.ContainsKey(pixels)) {
            try { CreateIconFont(pixels); }
            catch (Exception error) {
                iconFontFailed = true;
                Service.Log.Warning(error, "Lucide atlas unavailable; retaining contextual vector plates.");
                return null;
            }
        }
        var nearest = -1;
        foreach (var entry in iconFonts)
            if (entry.Value.Available && (nearest < 0 || Math.Abs(entry.Key - pixels) < Math.Abs(nearest - pixels))) nearest = entry.Key;
        return !iconFontFailed && nearest >= 0 ? iconDrawFonts[nearest] : null;
    }

    private void CreateIconFont(int pixels)
    {
        var handle = Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(toolkit => {
            try {
                using var stream = typeof(DalamudMapDrawContext).Assembly.GetManifestResourceStream("Gratis.Assets.Fonts.Lucide.lucide.ttf")
                    ?? throw new global::System.IO.FileNotFoundException("Lucide");
                toolkit.AddFontFromStream(stream, new SafeFontConfig { SizePx = pixels, OversampleH = 2, OversampleV = 1, PixelSnapH = true, GlyphRanges = GratisIcons.GlyphRanges }, false, "Lucide");
            }
            catch (Exception error) {
                iconFontFailed = true;
                Service.Log.Warning(error, "Lucide font unavailable; using primitive icons.");
            }
        }));
        iconFonts[pixels] = handle;
        iconDrawFonts[pixels] = new Font(handle);
    }

    public override string IconFontDiagnostics()
    {
        var text = new global::System.Text.StringBuilder($"icon-fonts failed={iconFontFailed} built=[");
        foreach (var size in TextDrawing.Sizes)
            if (iconFonts.TryGetValue(size, out var handle)) text.Append($"{size}:{(handle.Available ? "ready" : "pending")},");
        return text.Append(']').ToString();
    }

    public void Dispose()
    {
        foreach (var font in titleFonts.Values) font.Dispose();
        titleFonts.Clear();
        foreach (var font in iconFonts.Values) font.Dispose();
        iconFonts.Clear();
        iconDrawFonts.Clear();
    }

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
            var snap = Config.IsGratis;
            var size = snap ? TextDrawing.PixelSize(sizePx) : Math.Max(8, (int)(sizePx + 0.5f));
            var notoKey = (snap ? 20 : 1, size);
            if (!titleFonts.TryGetValue(notoKey, out var notoHandle)) {
                notoHandle = Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(toolkit => {
                    var config = new SafeFontConfig { SizePx = size };
                    if (snap) { config.OversampleH = 2; config.OversampleV = 1; config.PixelSnapH = true; }
                    toolkit.AddDalamudAssetFont(global::Dalamud.DalamudAsset.NotoSansCjkMedium, config);
                }));
                titleFonts[notoKey] = notoHandle;
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

    public override string TextDiagnostics() => base.TextDiagnostics() +
        $" atlasReady={(global::System.Linq.Enumerable.Count(titleFonts.Values, x => x.Available))}/{titleFonts.Count}" +
        $" requested=[{string.Join(",", titleFonts.Keys)}] lucide={iconFonts.Count} oversample=2x1 pixelSnapH=true";


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
