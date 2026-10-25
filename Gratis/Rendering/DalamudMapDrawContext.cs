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

    public override bool IsPvP => Service.ClientState.IsPvP;
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
            if (IsPvP || Service.ObjectTable.LocalPlayer is not { } player) return false;
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
    private IFontHandle? classicAxis;
    public override IDisposable PushLargeFont()
    {
        if (Config.IsGratis) return GetChromeFont(24, ChromeFontRole.Body)!.Push();
        classicAxis ??= Service.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new global::Dalamud.Interface.GameFonts.GameFontStyle {
            SizePt = 72, FamilyAndSize = global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Axis36,
            Italic = true, BaseSkewStrength = 16,
        });
        return classicAxis.Push();
    }
    private sealed class Font(global::Dalamud.Interface.ManagedFontAtlas.IFontHandle handle) : IDrawFont
    {
        public IDisposable Push() => handle.Push();
    }
    private readonly FontAtlasBudget fontBudget = new();
    private readonly FontAtlasBudget geistBudget = new(FontAtlasBudget.MaximumTextHandles, FontAtlasBudget.MaximumArea / 2);
    private readonly global::System.Collections.Generic.Dictionary<(int Choice, int Size), IFontHandle> titleFonts = [];
    private readonly global::System.Collections.Generic.Dictionary<IFontHandle, TextFont> textDrawFonts = [];
    private DefaultTextFont? defaultText;
    private bool fontFailureLogged;
    private int defaultFallbacks;
    private IDrawFont DefaultText => defaultText ??= new DefaultTextFont(this);
    private sealed class DefaultTextFont(DalamudMapDrawContext owner) : IDrawFont
    {
        public IDisposable Push() { owner.defaultFallbacks++; return Service.PluginInterface.UiBuilder.DefaultFontHandle.Push(); }
    }
    private sealed class TextFont(DalamudMapDrawContext owner, IFontHandle handle) : IDrawFont
    {
        public string Validity = "not-checked";
        public int Fallbacks;
        public unsafe IDisposable Push()
        {
            IDisposable? pushed = null;
            try {
                using var locked = handle.TryLock(out _);
                if (locked is not null && handle.LoadException is null) {
                    pushed = handle.Push();
                    if (ImGui.GetFont().Handle == locked.ImFont.Handle && DrawBinding.ValidTextFont(ImGui.GetFont())) {
                        Validity = "valid";
                        return pushed;
                    }
                    pushed.Dispose();
                    pushed = null;
                    Validity = "invalid-font/texture/ASCII";
                }
                else Validity = "pending/failed";
            }
            catch (Exception error) {
                pushed?.Dispose();
                Validity = "failed:" + error.GetType().Name;
                owner.LogFontFailure(error);
            }
            Fallbacks++;
            return owner.DefaultText.Push();
        }
    }
    private IDrawFont GuardText(IFontHandle? handle)
    {
        if (handle is null) return DefaultText;
        if (!textDrawFonts.TryGetValue(handle, out var cached)) textDrawFonts[handle] = cached = new TextFont(this, handle);
        return cached;
    }
    public override IDisposable PushDefaultTextFont() => DefaultText.Push();
    public override IDrawFont? GetTitleFont(int choice, float size)
    {
        if (Config.IsGratis) return GetChromeFont(size, ChromeFontRole.Body);
        try {
            var handle = GetNativeTitleFont(choice, size);
            return Config.IsGratis ? GuardText(handle) : handle is { Available: true } ? new Font(handle) : null;
        }
        catch (Exception error) { LogFontFailure(error); return Config.IsGratis ? DefaultText : null; }
    }
    public override IDrawFont? GetChromeFont(float size, ChromeFontRole role)
    {
        if (!Config.IsGratis) return null;
        try { return GetChromeFontCore(size, role); }
        catch (Exception error) {
            LogFontFailure(error);
            return DefaultText;
        }
    }
    private void LogFontFailure(Exception error)
    {
        if (fontFailureLogged) return;
        fontFailureLogged = true;
        Service.Log.Warning(error, "Sized text unavailable; using the default Dalamud UI font.");
    }
    private IDrawFont GetChromeFontCore(float size, ChromeFontRole role)
    {
        var pixels = TextDrawing.PixelSize(size);
        var key = (10, pixels);
        if (!titleFonts.TryGetValue(key, out var handle)) {
            if (!geistBudget.Reserve(key.Item1, pixels, FontAtlasBudget.TextGlyphs)) return DefaultText;
            handle = CreateTextFont(pixels);
            titleFonts[key] = handle;
        }
        return GuardText(handle);
    }
    private IFontHandle CreateTextFont(int pixels)
    {
        const string filename = "Geist-Regular.ttf";
        return Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(toolkit => {
            using var stream = typeof(DalamudMapDrawContext).Assembly.GetManifestResourceStream($"Gratis.Assets.Fonts.{filename}")
                ?? throw new global::System.IO.FileNotFoundException(filename);
            toolkit.Font = toolkit.AddFontFromStream(stream,
                new SafeFontConfig { SizePx = pixels, OversampleH = 1, OversampleV = 1, PixelSnapH = true, GlyphRanges = FontAtlasBudget.TextRanges }, false, filename);
        }));
    }

    private readonly global::System.Collections.Generic.Dictionary<int, IFontHandle> iconFonts = [];
    private readonly global::System.Collections.Generic.Dictionary<int, Font> iconDrawFonts = [];
    private bool iconFontFailed;

    public override IDrawFont? GetIconFont(float size)
    {
        if (iconFontFailed) return null;
        var pixels = TextDrawing.IconPixelSize(size);
        if (!iconFonts.ContainsKey(pixels)) {
            if (!fontBudget.Reserve(100, pixels, GratisIcons.GlyphRanges.Length / 2)) return NearestIconFont(pixels);
            try { CreateIconFont(pixels); }
            catch (Exception error) {
                iconFontFailed = true;
                Service.Log.Warning(error, "Lucide atlas unavailable; retaining contextual vector plates.");
                return null;
            }
        }
        return NearestIconFont(pixels);
    }

    private IDrawFont? NearestIconFont(int pixels)
    {
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
                toolkit.AddFontFromStream(stream, new SafeFontConfig { SizePx = pixels, OversampleH = 1, OversampleV = 1, PixelSnapH = true, GlyphRanges = GratisIcons.GlyphRanges }, false, "Lucide");
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
        foreach (var size in TextDrawing.IconSizes)
            if (iconFonts.TryGetValue(size, out var handle)) text.Append($"{size}:{(handle.Available ? "ready" : "pending")},");
        return text.Append(']').ToString();
    }

    public void Dispose()
    {
        classicAxis?.Dispose();
        foreach (var font in titleFonts.Values) font.Dispose();
        titleFonts.Clear();
        textDrawFonts.Clear();
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
                if (!fontBudget.Reserve(notoKey.Item1, size, FontAtlasBudget.TextGlyphs)) return null;
                notoHandle = CreateNotoFont(size, snap);
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
            if (!fontBudget.Reserve(fontChoice, (int)best, 0)) return null;
            handle = Service.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new global::Dalamud.Interface.GameFonts.GameFontStyle(best));
            titleFonts[key] = handle;
        }

        return handle;
    }

    private IFontHandle CreateNotoFont(int size, bool snap) =>
        Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(toolkit => {
            var config = new SafeFontConfig { SizePx = size };
            if (snap) { config.GlyphRanges = FontAtlasBudget.TextRanges; config.OversampleH = 1; config.OversampleV = 1; config.PixelSnapH = true; }
            toolkit.Font = toolkit.AddDalamudAssetFont(global::Dalamud.DalamudAsset.NotoSansCjkMedium, config);
        }));

    public override string TextDiagnostics()
    {
        var message = new global::System.Text.StringBuilder(base.TextDiagnostics());
        long iconArea = 0;
        foreach (var size in iconFonts.Keys) iconArea += FontAtlasBudget.Estimate(size, GratisIcons.GlyphRanges.Length / 2);
        var totalArea = geistBudget.EstimatedArea + iconArea;
        message.Append($" handles={geistBudget.Count}/{FontAtlasBudget.MaximumTextHandles} allOwnedHandles={titleFonts.Count + iconFonts.Count} estimatedArea={totalArea}/{FontAtlasBudget.MaximumArea}px textArea={geistBudget.EstimatedArea} iconArea={iconArea} budgetWarning={geistBudget.Denied > 0 || totalArea > FontAtlasBudget.MaximumArea / 2} denied={geistBudget.Denied} iconLegacyDenied={fontBudget.Denied} defaultFallbacks={defaultFallbacks} oversample=1x1 neverEvict=true");
        foreach (var pair in titleFonts) {
            textDrawFonts.TryGetValue(pair.Value, out var font);
            var ranges = pair.Key.Choice == 1 ? "legacy" : pair.Key.Choice is >= 2 and <= 5 ? "game-bitmap" : "0020-00FF,2000-206F";
            message.Append($"; text({pair.Key.Choice},{pair.Key.Size}) built={pair.Value.Available} loadError={pair.Value.LoadException?.GetType().Name ?? "none"} ranges={ranges} validity={font?.Validity ?? "not-checked"} fallbacks={font?.Fallbacks ?? 0}");
        }
        return message.Append("; ").Append(IconFontDiagnostics()).ToString();
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
