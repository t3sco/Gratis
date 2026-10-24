using System.Drawing;
using System.Numerics;
using Dalamud.Interface.ManagedFontAtlas;
using Math = global::System.Math;
using MathF = global::System.MathF;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Classes;
using KamiLib.CommandManager;
using KamiLib.Window;
using Lumina.Excel.Sheets;
using Gratis.Classes;
using Gratis.Classes.MapWindowComponents;
using Gratis.Controllers;
using Gratis.Data;
using Map = Lumina.Excel.Sheets.Map;

namespace Gratis.Windows;

public class MapWindow : Window
{
    public Vector2 MapDrawOffset { get; private set; }
    public HoverFlags HoveredFlags { get; private set; }
    public bool ProcessingCommand { get; set; }

    private bool isDragStarted;
    private Vector2 lastWindowSize;
    private uint lastMapId;
    private uint lastAreaPlaceNameId;
    private uint lastSubAreaPlaceNameId;

    private readonly MapToolbar mapToolbar = new();
    private readonly MapCoordinateBar mapCoordinateBar = new();
    private readonly MapContextMenu mapContextMenu = new();

    public MapWindow() : base("###GratisMapWindow", new Vector2(400.0f, 250.0f))
    {
        UpdateTitle();

        DisableWindowSounds = true;
        RegisterCommands();
    }

    public override bool DrawConditions() => IntegrationsController.ShouldShowMap();

    public override unsafe void PreOpenCheck()
    {
        if (System.MapWindow.IsOpen && AgentMap.Instance()->AddonId is 0)
        {
            Service.Log.Debug("[OnShow] MapWindow can not be open now.");
            IsOpen = false;
        }
        
        if (System.SystemConfig.KeepOpen) {
            IsOpen = true;
        }

        if (Service.ClientState is { IsLoggedIn: false } or { IsPvP: true }) IsOpen = false;
    }

    public override void OnOpen()
    {
        if (ProcessingCommand) {
            ProcessingCommand = false;
            System.SystemConfig.FollowPlayer = false;
            return;
        }

        if (System.SystemConfig.FollowOnOpen) {
            System.IntegrationsController.OpenOccupiedMap();
            System.SystemConfig.FollowPlayer = true;
        }

        switch (System.SystemConfig.CenterOnOpen) {
            case CenterTarget.Player when Service.ObjectTable.LocalPlayer is { } localPlayer:
                System.MapRenderer.CenterOnGameObject(localPlayer);
                break;

            case CenterTarget.Map:
                System.SystemConfig.FollowPlayer = false;
                System.MapRenderer.DrawOffset = Vector2.Zero;
                break;

            case CenterTarget.Disabled:
            default:
                break;
        }
    }

    protected override void DrawContents()
    {
        UpdateTitle();
        UpdateStyle();
        UpdateSizePosition();

        HoveredFlags = HoverFlags.Nothing;

        if (WindowBounds.IsBoundedBy(ImGui.GetMousePos(), ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + ImGui.GetContentRegionMax())) {
            HoveredFlags |= HoverFlags.Window;
        }

        if (System.SystemConfig.BlurBackground) {
            var blurMin = ImGui.GetCursorScreenPos();
            ImGuiHelpers.PrependBlurBehind(ImGui.GetWindowDrawList(), blurMin, blurMin + ImGui.GetContentRegionAvail(), System.SystemConfig.BlurStrength, MapShape.Rounding(ImGui.GetContentRegionAvail()), Vector4.Zero, Vector4.Zero, 0.0f);
        }

        MapDrawOffset = ImGui.GetCursorScreenPos();
        using var fade = ImRaii.PushStyle(ImGuiStyleVar.Alpha, System.SystemConfig.FadePercent, ShouldFade());
        using (var renderChild = ImRaii.Child("render_child", ImGui.GetContentRegionAvail(), false, ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar)) {
            if (!renderChild) return;
            MapShape.Update(ImGui.GetWindowSize());
            if (!System.SystemConfig.AcceptedSpoilerWarning) {
                DrawSpoilerWarning();
                return;
            }

            DrawMapElements();

            ImGui.SetCursorPos(Vector2.Zero);
            DrawToolbar();
            DrawCoordinateBar();
            DrawFrameEffects();
            DrawTitleOnMap();
        }

        if (ImGui.IsItemHovered()) {
            HoveredFlags |= HoverFlags.WindowInnerFrame;
        }

        ProcessInputs();
    }

    private void DrawMapElements()
    {
        System.MapRenderer.DrawBaseTexture();
        if (ImGui.IsItemHovered()) {
            HoveredFlags |= HoverFlags.MapTexture;
        }

        System.MapRenderer.DrawDynamicElements();
    }

    private void DrawToolbar()
    {
        if (!ShouldShowToolbar()) return;

        using (ImRaii.Group()) {
            mapToolbar.Draw();
        }

        if (ImGui.IsItemHovered()) {
            HoveredFlags |= HoverFlags.Toolbar;
        }
    }

    private void DrawCoordinateBar()
    {
        if (!System.SystemConfig.ShowCoordinateBar) return;

        using (ImRaii.Group()) {
            mapCoordinateBar.Draw(HoveredFlags.HasFlag(HoverFlags.MapTexture), MapDrawOffset);
        }

        if (ImGui.IsItemHovered()) {
            HoveredFlags |= HoverFlags.CoordinateBar;
        }
    }

    private static uint WithAlpha(Vector4 color, float alphaScale)
    {
        color.W *= alphaScale;
        return ImGui.ColorConvertFloat4ToU32(color);
    }

    private void DrawFrameEffects()
    {
        var config = System.SystemConfig;
        var scale = ImGuiHelpers.GlobalScale;
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        var rounding = MapShape.Current;
        var drawList = ImGui.GetWindowDrawList();

        if (config.MapVignette > 0.0f) {
            var depth = MathF.Min(ImGui.GetWindowSize().X, ImGui.GetWindowSize().Y) * 0.2f;
            DrawGradientRing(drawList, min, max, rounding, depth, new Vector4(0.0f, 0.0f, 0.0f, config.MapVignette * 0.75f), 2.0f);
        }

        if (config.MapGlow) {
            var pulse = 1.0f + MathF.Sin((float)ImGui.GetTime() * config.MapGlowSpeed * 2.0f) * config.MapGlowPulse * 0.5f;
            var color = config.MapGlowColor;
            color.W = Math.Clamp(color.W * pulse, 0.0f, 1.0f);
            DrawGradientRing(drawList, min, max, rounding, config.MapGlowSize * scale, color, 2.0f);
        }

        if (config.MapBorder) {
            var half = config.MapBorderThickness * scale * 0.5f;
            drawList.AddRect(min + new Vector2(half), max - new Vector2(half), ImGui.ColorConvertFloat4ToU32(config.MapBorderColor), MathF.Max(0.0f, rounding - half), ImDrawFlags.None, config.MapBorderThickness * scale);
        }
    }

    private static readonly global::System.Collections.Generic.List<Vector2> ringOuter = [];
    private static readonly global::System.Collections.Generic.List<Vector2> ringInner = [];

    private static void RoundedRectPoints(Vector2 min, Vector2 max, float radius, global::System.Collections.Generic.List<Vector2> points)
    {
        points.Clear();
        radius = MathF.Max(0.0f, MathF.Min(radius, MathF.Min(max.X - min.X, max.Y - min.Y) * 0.5f));
        const int segments = 10;
        var centers = new[] { new Vector2(min.X + radius, min.Y + radius), new Vector2(max.X - radius, min.Y + radius), new Vector2(max.X - radius, max.Y - radius), new Vector2(min.X + radius, max.Y - radius) };
        for (var corner = 0; corner < 4; corner++) {
            var startAngle = MathF.PI * (1.0f + corner * 0.5f);
            for (var step = 0; step <= segments; step++) {
                var angle = startAngle + MathF.PI * 0.5f * step / segments;
                points.Add(centers[corner] + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
            }
        }
    }

    private static void DrawGradientRing(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float depth, Vector4 color, float power)
    {
        const int bands = 8;
        var white = ImGui.GetFontTexUvWhitePixel();
        for (var band = 0; band < bands; band++) {
            var t0 = band / (float)bands;
            var t1 = (band + 1) / (float)bands;
            RoundedRectPoints(min + new Vector2(depth * t0), max - new Vector2(depth * t0), rounding - depth * t0, ringOuter);
            RoundedRectPoints(min + new Vector2(depth * t1), max - new Vector2(depth * t1), rounding - depth * t1, ringInner);
            var count = ringOuter.Count;
            var outerColor = WithAlpha(color, MathF.Pow(1.0f - t0, power));
            var innerColor = WithAlpha(color, MathF.Pow(1.0f - t1, power));

            drawList.PrimReserve(count * 6, count * 2);
            var baseIndex = (ushort)drawList.VtxCurrentIdx;
            for (var index = 0; index < count; index++) drawList.PrimWriteVtx(ringOuter[index], white, outerColor);
            for (var index = 0; index < count; index++) drawList.PrimWriteVtx(ringInner[index], white, innerColor);
            for (var index = 0; index < count; index++) {
                var next = (index + 1) % count;
                drawList.PrimWriteIdx((ushort)(baseIndex + index));
                drawList.PrimWriteIdx((ushort)(baseIndex + next));
                drawList.PrimWriteIdx((ushort)(baseIndex + count + next));
                drawList.PrimWriteIdx((ushort)(baseIndex + index));
                drawList.PrimWriteIdx((ushort)(baseIndex + count + next));
                drawList.PrimWriteIdx((ushort)(baseIndex + count + index));
            }
        }
    }

    private int pushedGripColors;

    public override void PreDraw()
    {
        pushedGripColors = 0;
        if (IsFocused || HoveredFlags.Any()) return;

        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, Vector4.Zero);
        pushedGripColors = 3;
    }

    public override void PostDraw()
    {
        if (pushedGripColors > 0) ImGui.PopStyleColor(pushedGripColors);
        pushedGripColors = 0;
    }

    private readonly global::System.Collections.Generic.Dictionary<(int Choice, int Size), global::Dalamud.Interface.ManagedFontAtlas.IFontHandle> titleFonts = [];

    private static readonly global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize[][] GameFontPresets = [
        [global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Axis96, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Axis12, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Axis14, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Axis18, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Axis36],
        [global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Meidinger16, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Meidinger20, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Meidinger40],
        [global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.TrumpGothic184, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.TrumpGothic23, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.TrumpGothic34, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.TrumpGothic68],
        [global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Jupiter16, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Jupiter20, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Jupiter23, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Jupiter45, global::Dalamud.Interface.GameFonts.GameFontFamilyAndSize.Jupiter90],
    ];

    private global::Dalamud.Interface.ManagedFontAtlas.IFontHandle? GetTitleFont(int fontChoice, float sizePx)
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

    private void DrawTitleOnMap()
    {
        var config = System.SystemConfig;
        if (!config.HideWindowFrame || !config.ShowTitleOnMap) return;

        var title = WindowName;
        var idIndex = title.IndexOf("###");
        if (idIndex >= 0) title = title[..idIndex];
        if (string.IsNullOrWhiteSpace(title)) return;

        var parts = title.Split(" - ", global::System.StringSplitOptions.RemoveEmptyEntries | global::System.StringSplitOptions.TrimEntries);
        var lines = new global::System.Collections.Generic.List<(string Text, float Scale, Vector4 Color)>();
        if (config.TitleStacked && parts.Length > 1) {
            var headline = (parts[^1], config.TitleScale, config.TitleColor);
            var secondary = (string.Join(config.TitleSeparator, parts[..^1]), config.TitleScale * config.TitleSecondaryScale, config.TitleSecondaryColor);
            if (config.TitleSecondaryAbove) {
                lines.Add(secondary);
                lines.Add(headline);
            }
            else {
                lines.Add(headline);
                lines.Add(secondary);
            }
        }
        else {
            lines.Add((string.Join(config.TitleSeparator, parts), config.TitleScale, config.TitleColor));
        }

        var scale = ImGuiHelpers.GlobalScale;
        var baseFontSize = ImGui.GetFontSize();
        var spacing = config.TitleLineSpacing * scale;
        var sizes = new Vector2[lines.Count];
        var textSize = Vector2.Zero;
        var handles = new global::Dalamud.Interface.ManagedFontAtlas.IFontHandle?[lines.Count];
        for (var index = 0; index < lines.Count; index++) {
            handles[index] = GetTitleFont(config.TitleFont, baseFontSize * lines[index].Scale);
            using (handles[index]?.Push()) {
                sizes[index] = ImGui.CalcTextSize(lines[index].Text) * (handles[index] is null ? lines[index].Scale : 1.0f);
            }

            textSize.X = sizes[index].X > textSize.X ? sizes[index].X : textSize.X;
            textSize.Y += sizes[index].Y + (index > 0 ? spacing : 0.0f);
        }

        var padding = config.TitleBackground ? config.TitlePadding * scale : Vector2.Zero;
        var boxSize = textSize + padding * 2.0f;
        var offset = config.TitleOffset * scale;

        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        var isRight = config.TitleCorner is 1 or 3;
        var isBottom = config.TitleCorner is 2 or 3;
        var x = isRight ? windowSize.X - boxSize.X - offset.X : offset.X;
        var y = isBottom ? windowSize.Y - boxSize.Y - offset.Y : offset.Y;
        if (!isBottom && config.TitleBelowToolbar && isRight) y += ImGui.GetFrameHeight();

        var boxMin = windowPos + new Vector2(x, y);
        var boxMax = boxMin + boxSize;
        var drawList = ImGui.GetWindowDrawList();

        if (config.TitleBackground) {
            var rounding = config.TitleRounding * scale;
            drawList.AddRectFilled(boxMin, boxMax, ImGui.ColorConvertFloat4ToU32(config.TitleBackgroundColor), rounding);
            if (config.TitleBorder) {
                drawList.AddRect(boxMin, boxMax, ImGui.ColorConvertFloat4ToU32(config.TitleBorderColor), rounding, ImDrawFlags.None, 1.0f);
            }
        }

        var font = ImGui.GetFont();
        var outline = ImGui.ColorConvertFloat4ToU32(config.TitleOutlineColor);
        var cursorY = boxMin.Y + padding.Y;
        for (var index = 0; index < lines.Count; index++) {
            var line = lines[index];
            var lineX = isRight ? boxMax.X - padding.X - sizes[index].X : boxMin.X + padding.X;
            var linePosition = new Vector2(lineX, cursorY);
            using (handles[index]?.Push()) {
                var lineFont = handles[index] is null ? font : ImGui.GetFont();
                var fontSize = handles[index] is null ? baseFontSize * line.Scale : ImGui.GetFontSize();
                var color = ImGui.ColorConvertFloat4ToU32(line.Color);
                var bold = new Vector2(config.TitleBold, 0.0f);
                if (config.TitleOutline) {
                    foreach (var step in new[] { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1), new Vector2(0, 1) }) {
                        drawList.AddText(lineFont, fontSize, linePosition + step, outline, line.Text);
                        if (config.TitleBold > 0.0f) drawList.AddText(lineFont, fontSize, linePosition + step + bold, outline, line.Text);
                    }
                }

                drawList.AddText(lineFont, fontSize, linePosition, color, line.Text);
                if (config.TitleBold > 0.0f) drawList.AddText(lineFont, fontSize, linePosition + bold, color, line.Text);
            }

            cursorY += sizes[index].Y + spacing;
        }
    }

    private bool ShouldShowToolbar()
    {
        if (isDragStarted) return false;
        if (System.SystemConfig.ShowToolbarOnHover && HoveredFlags.Any()) return true;
        if (System.SystemConfig.AlwaysShowToolbar) return true;

        return false;
    }

    private unsafe void UpdateTitle()
    {
        var mapChanged = lastMapId != AgentMap.Instance()->SelectedMapId;
        var areaChanged = lastAreaPlaceNameId != TerritoryInfo.Instance()->AreaPlaceNameId;
        var subAreaChanged = lastSubAreaPlaceNameId != TerritoryInfo.Instance()->SubAreaPlaceNameId;
        var locationChanged = mapChanged || areaChanged || subAreaChanged;

        if (!locationChanged) return;
        var subLocationString = string.Empty;
        var mapData = Service.DataManager.GetExcelSheet<Map>().GetRow(AgentMap.Instance()->SelectedMapId);

        if (System.SystemConfig.ShowRegionLabel) {
            var mapRegionName = mapData.PlaceNameRegion.Value.Name.ExtractText();
            subLocationString += $" - {mapRegionName}";
        }

        if (System.SystemConfig.ShowMapLabel) {
            var mapName = mapData.PlaceName.Value.Name.ExtractText();
            subLocationString += $" - {mapName}";
        }

        if (AgentMap.Instance()->SelectedMapId == AgentMap.Instance()->CurrentMapId) {
            if (TerritoryInfo.Instance()->AreaPlaceNameId is not 0 && System.SystemConfig.ShowAreaLabel) {
                var areaLabel = Service.DataManager.GetExcelSheet<PlaceName>().GetRow(TerritoryInfo.Instance()->AreaPlaceNameId);
                subLocationString += $" - {areaLabel.Name}";
            }

            if (TerritoryInfo.Instance()->SubAreaPlaceNameId is not 0 && System.SystemConfig.ShowSubAreaLabel) {
                var subAreaLabel = Service.DataManager.GetExcelSheet<PlaceName>().GetRow(TerritoryInfo.Instance()->SubAreaPlaceNameId);
                subLocationString += $" - {subAreaLabel.Name}";
            }
        }

        WindowName = $"{subLocationString}###GratisMapWindow".TrimStart(['-',' ']);

        lastMapId = AgentMap.Instance()->SelectedMapId;
        lastAreaPlaceNameId = TerritoryInfo.Instance()->AreaPlaceNameId;
        lastSubAreaPlaceNameId = TerritoryInfo.Instance()->SubAreaPlaceNameId;
    }

    public void RefreshTitle()
    {
        lastMapId = 0;
        lastAreaPlaceNameId = 0;
        lastSubAreaPlaceNameId = 0;
    }

    private void ProcessInputs()
    {
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
            ImGui.OpenPopup("Gratis_Context_Menu");
        }
        else {
            if (HoveredFlags.Any()) {
                if (System.SystemConfig.EnableShiftDragMove && ImGui.GetIO().KeyShift) {
                    Flags &= ~ImGuiWindowFlags.NoMove;
                }
                else {
                    ProcessMouseScroll();
                    ProcessMapDragStart();
                    Flags |= ImGuiWindowFlags.NoMove;
                }
            }

            ProcessMapDragDragging();
            ProcessMapDragEnd();
        }

        mapContextMenu.Draw(MapDrawOffset);
    }

    private unsafe void UpdateStyle()
    {
        if (System.SystemConfig.HideWindowFrame) {
            Flags |= ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground;
        }
        else {
            Flags &= ~(ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground);
        }

        if (System.SystemConfig.LockWindow) {
            Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;
        }
        else {
            Flags &= ~(ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);
        }

        if (System.SystemConfig.NoFocusOnAppear) {
            Flags |= ImGuiWindowFlags.NoFocusOnAppearing;
        }
        else {
            Flags &= ~ImGuiWindowFlags.NoFocusOnAppearing;
        }

        if (System.SystemConfig.HideWindowBackground) {
            Flags |= ImGuiWindowFlags.NoBackground;
        }
        else {
            Flags &= ~ImGuiWindowFlags.NoBackground;
        }

        if (Service.KeyState[VirtualKey.ESCAPE] && IsFocused && !IsMapLocked()) {
            AgentMap.Instance()->Hide();
        }

        if (System.SystemConfig.FollowPlayer && Service.ObjectTable is { LocalPlayer: { } localPlayer }) {
            System.MapRenderer.CenterOnGameObject(localPlayer);
        }

        if (System.SystemConfig.LockCenterOnMap) {
            System.SystemConfig.FollowPlayer = false;
            System.MapRenderer.DrawOffset = Vector2.Zero;
        }
    }

    private void UpdateSizePosition()
    {
        var systemConfig = System.SystemConfig;
        var windowPosition = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();

        if (!IsFocused) {
            if (windowPosition != systemConfig.WindowPosition) {
                ImGui.SetWindowPos(systemConfig.WindowPosition);
            }

            if (windowSize != systemConfig.WindowSize) {
                ImGui.SetWindowSize(systemConfig.WindowSize);
            }
        }
        else {
            if (systemConfig.WindowPosition != windowPosition) {
                systemConfig.WindowPosition = windowPosition;
                SystemConfig.Save();
            }

            if (systemConfig.WindowSize != windowSize) {
                systemConfig.WindowSize = windowSize;
                SystemConfig.Save();
            }
        }
    }

    private static void DrawSpoilerWarning()
    {
        using (ImRaii.PushColor(ImGuiCol.Text, KnownColor.Orange.Vector())) {
            const string warningLine1 = "Warning, Gratis does not protect you from spoilers and will show everything.";
            const string warningLine2 = "Do not use Gratis if you are not comfortable with this.";

            ImGui.SetCursorPos(ImGui.GetContentRegionAvail() / 2.0f - (ImGui.CalcTextSize(warningLine1) * 2.0f) with { X = 0.0f });
            ImGuiHelpers.CenteredText(warningLine1);
            ImGuiHelpers.CenteredText(warningLine2);
        }

        ImGuiHelpers.ScaledDummy(30.0f);
        ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X / 3.0f);
        using (ImRaii.Disabled(!(ImGui.GetIO().KeyShift && ImGui.GetIO().KeyCtrl))) {
            if (ImGui.Button("I understand", new Vector2(ImGui.GetContentRegionAvail().X / 2.0f, 23.0f * ImGuiHelpers.GlobalScale))) {
                System.SystemConfig.AcceptedSpoilerWarning = true;
                SystemConfig.Save();
            }

            using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 1.0f)) {
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
                    ImGui.SetTooltip("Hold Shift + Control while clicking activate button");
                }
            }
        }
    }

    public override unsafe void OnClose()
    {
        AgentMap.Instance()->Hide();

        SystemConfig.Save();
    }

    private void ProcessMouseScroll()
    {
        if (System.SystemConfig.ZoomLocked) return;
        if (ImGui.GetIO().MouseWheel is 0) return;
        if (!HoveredFlags.HasFlag(HoverFlags.WindowInnerFrame)) return;

        if (System.SystemConfig.UseLinearZoom) {
            MapRenderer.MapRenderer.Scale += System.SystemConfig.ZoomSpeed * ImGui.GetIO().MouseWheel;
        }
        else {
            MapRenderer.MapRenderer.Scale *= 1.0f + System.SystemConfig.ZoomSpeed * ImGui.GetIO().MouseWheel;
        }
    }

    private void ProcessMapDragDragging()
    {
        if (ImGui.IsMouseDragging(ImGuiMouseButton.Left) && isDragStarted) {
            System.MapRenderer.DrawOffset += ImGui.GetMouseDragDelta() / MapRenderer.MapRenderer.Scale;
            ImGui.ResetMouseDragDelta();
        }
    }

    private void ProcessMapDragEnd()
    {
        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left)) {
            isDragStarted = false;
        }
    }

    private void ProcessMapDragStart()
    {
        if (ImGui.GetWindowSize() == lastWindowSize) {
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left) && !isDragStarted) {
                isDragStarted = true;
                System.SystemConfig.FollowPlayer = false;
            }
        }
        else {
            lastWindowSize = ImGui.GetWindowSize();
            isDragStarted = false;
        }
    }

    private unsafe bool ShouldFade() =>
        System.SystemConfig.FadeMode.HasFlag(FadeMode.Always) ||
        System.SystemConfig.FadeMode.HasFlag(FadeMode.WhenFocused) && IsFocused ||
        System.SystemConfig.FadeMode.HasFlag(FadeMode.WhenMoving) && AgentMap.Instance()->IsPlayerMoving ||
        System.SystemConfig.FadeMode.HasFlag(FadeMode.WhenUnFocused) && !IsFocused;

    private void RegisterCommands()
    {
        System.CommandManager.RegisterCommand(new ToggleCommandHandler
        {
            UseShowHideText = true,
            BaseActivationPath = "/map",
            EnableDelegate = _ => System.MapWindow.UnCollapseOrShow(),
            DisableDelegate = _ => System.MapWindow.Close(),
            ToggleDelegate = _ => System.MapWindow.UnCollapseOrToggle(),
        });

        System.CommandManager.RegisterCommand(new CommandHandler
        {
            ActivationPath = "/map/follow",
            Delegate = _ =>
            {
                System.SystemConfig.FollowPlayer = true;
                SystemConfig.Save();
            },
        });

        System.CommandManager.RegisterCommand(new CommandHandler
        {
            ActivationPath = "/map/unfollow",
            Delegate = _ =>
            {
                System.SystemConfig.FollowPlayer = false;
                SystemConfig.Save();
            },
        });

        System.CommandManager.RegisterCommand(new ToggleCommandHandler
        {
            BaseActivationPath = "/autofollow",
            EnableDelegate = _ =>
            {
                System.SystemConfig.FollowOnOpen = true;
                SystemConfig.Save();
            },
            DisableDelegate = _ =>
            {
                System.SystemConfig.FollowOnOpen = false;
                SystemConfig.Save();
            },
            ToggleDelegate = _ =>
            {
                System.SystemConfig.FollowOnOpen = !System.SystemConfig.FollowOnOpen;
                SystemConfig.Save();
            },
        });

        System.CommandManager.RegisterCommand(new ToggleCommandHandler
        {
            BaseActivationPath = "/keepopen",
            EnableDelegate = _ =>
            {
                System.SystemConfig.KeepOpen = true;
                SystemConfig.Save();
            },
            DisableDelegate = _ =>
            {
                System.SystemConfig.KeepOpen = false;
                SystemConfig.Save();
            },
            ToggleDelegate = _ =>
            {
                System.SystemConfig.KeepOpen = !System.SystemConfig.KeepOpen;
                SystemConfig.Save();
            },
        });

        System.CommandManager.RegisterCommand(new CommandHandler
        {
            ActivationPath = "/center/player",
            Delegate = _ =>
            {
                if (Service.ObjectTable.LocalPlayer is { } localPlayer) {
                    System.MapRenderer.CenterOnGameObject(localPlayer);
                }
            },
        });

        System.CommandManager.RegisterCommand(new CommandHandler
        {
            ActivationPath = "/center/map",
            Delegate = _ =>
            {
                System.SystemConfig.FollowPlayer = false;
                System.MapRenderer.DrawOffset = Vector2.Zero;
            },
        });
    }

    private static unsafe bool IsMapLocked()
    {
        var addon = Service.GameGui.GetAddonByName<AddonAreaMap>("AreaMap");
        if (addon is null || addon->RootNode is null) return false;

        return (addon->Param & 0x8_0000) > 0;
    }
}