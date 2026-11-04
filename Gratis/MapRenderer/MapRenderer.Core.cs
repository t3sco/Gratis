using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Data.Files;
using Gratis.Classes;

namespace Gratis.MapRenderer;

public unsafe partial class MapRenderer : Rendering.IMapRendererHost, IDisposable {
    public static float Scale {
        get => System.SystemConfig.MapScale;
        set => WriteScale(value, new global::System.Diagnostics.StackFrame(1).GetMethod()?.Name ?? "external");
    }

    private readonly Rendering.MapRendererFrame frame;
    public Rendering.MapRendererFrame Frame => frame;
    public Vector2 DrawOffset {
        get => frame.DrawOffset; set => frame.CenterOnOffset(value, false);
    }

    private static void WriteScale(float value,
        [global::System.Runtime.CompilerServices.CallerMemberName] string writer = "") {
        if (System.MapRenderer is { } renderer) {
            renderer.frame.SetViewportScale(value, false);
            renderer.frame.viewportMotion.Written("scale", writer);
        }
        else
            System.SystemConfig.MapScale = value;
    }

    public Vector2 DrawPosition => frame.DrawPosition;

    private IDalamudTextureWrap? blendedTexture;
    private string blendedPath = string.Empty;
    private Task<Rendering.MapLayerPixels?>? blendedWork;
    private readonly MapContentCache mapContents = new(path => LoadMapPixels(path));
    private Rendering.MapContentBounds? contentBounds;
    private bool nativeTextureReady;
    private long viewSession = global::System.Diagnostics.Stopwatch.GetTimestamp();
    private void OnViewTerritoryChanged(uint territory) =>
        global::System.Threading.Interlocked.Increment(ref viewSession);

    public MapRenderer() {
        cameraHeading = new CameraHeadingReader();
        frame = new Rendering.MapRendererFrame(this);
        Service.ClientState.TerritoryChanged += OnViewTerritoryChanged;
    }

    public void Dispose() {
        Service.ClientState.TerritoryChanged -= OnViewTerritoryChanged;
        cameraHeading.Dispose();
        mapColours.Dispose();
        mapContents.Dispose();
        blendedTexture?.Dispose();
    }

    public void CenterOnGameObject(IGameObject obj) =>
        frame.CenterOnCoordinate(new Vector2(obj.Position.X, obj.Position.Z), false);

    public void CenterOnCoordinate(Vector2 coord) => frame.CenterOnCoordinate(coord);

    private static Vector2 CoordinateOffset(Vector2 coord) => -coord * DrawHelpers.GetMapScaleFactor()
        + DrawHelpers.GetMapOffsetVector();

    public void DrawBaseTexture() => frame.DrawBaseTexture();
    public void DrawDynamicElements() => frame.DrawDynamicElements();

    Data.SystemConfig Rendering.IMapRendererHost.Config => System.SystemConfig;
    double Rendering.IMapRendererHost.Now => ImGui.GetTime();
    int Rendering.IMapRendererHost.FrameId => ImGui.GetFrameCount();
    uint Rendering.IMapRendererHost.MapId => AgentMap.Instance()->SelectedMapId;
    // Keyed on the selected map's own territory so a remote map keeps one stable key wherever the player stands.
    ulong Rendering.IMapRendererHost.MapKey => ((ulong)AgentMap.Instance()->SelectedTerritoryId << 32)
        | AgentMap.Instance()->SelectedMapId;
    Rendering.MapContentBounds? Rendering.IMapRendererHost.ContentBounds => contentBounds;
    bool Rendering.IMapRendererHost.SmallMapHint => DrawHelpers.GetMapScaleFactor() >= 2;
    bool Rendering.IMapRendererHost.TextureReady => nativeTextureReady;
    bool Rendering.IMapRendererHost.InstanceMap =>
        Service.Condition[global::Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty];
    long Rendering.IMapRendererHost.ViewSession => viewSession;
    Vector2 Rendering.IMapRendererHost.CoordinateOffset(Vector2 position) => CoordinateOffset(position);
    void Rendering.IMapRendererHost.DrawBackgroundTexture() {
        using var image = Rendering.BenchmarkProbe.Measure(Rendering.BenchmarkSection.MapImage);
        DrawBackgroundTexture();
    }
    void Rendering.IMapRendererHost.DrawFlag() => DrawFlag();
    void Rendering.IMapRendererHost.DrawPlayer() {
        using var arrow = Rendering.BenchmarkProbe.Measure(Rendering.BenchmarkSection.PlayerArrow);
        DrawPlayer();
    }
    void Rendering.IMapRendererHost.DrawTextMarkers() => DrawStaticTextMarkers();
    void Rendering.IMapRendererHost.CollectMarkers() {
        Rendering.MapDrawContext.Current.SynchronizePvp();
        using (Rendering.BenchmarkProbe.Measure(Rendering.BenchmarkSection.Enemies)) DrawEnemies();
        using (Rendering.BenchmarkProbe.Measure(Rendering.BenchmarkSection.StaticMarkers)) DrawStaticMapMarkers();
        using (Rendering.BenchmarkProbe.Measure(Rendering.BenchmarkSection.DynamicMarkers)) DrawDynamicMarkers();
        using (Rendering.BenchmarkProbe.Measure(Rendering.BenchmarkSection.GameObjects)) DrawGameObjects();
        using (Rendering.BenchmarkProbe.Measure(Rendering.BenchmarkSection.GroupMembers)) DrawGroupMembers();
        using (Rendering.BenchmarkProbe.Measure(Rendering.BenchmarkSection.OtherMarkers)) {
            DrawTemporaryMarkers();
            if (!Service.ClientState.IsPvP)
                DrawGatheringMarkers();
            DrawFieldMarkers();
            DrawAetherCurrentMarkers();
        }
        DrawEnemyTooltip();
    }

    private void DrawBackgroundTexture() {
        nativeTextureReady = false;
        var agent = AgentMap.Instance();
        var changing = !Service.ClientState.IsLoggedIn || Service.ObjectTable.LocalPlayer == null
            || Service.Condition[global::Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]
            || Service.Condition[global::Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51];
        var hasMap = agent->SelectedMapId != 0;
        var hasPath = agent->SelectedMapPath.Length > 0;
        if (!hasMap || !hasPath) {
            // A zero selection during startup is not proof that the area has no map.
            var territory = Service.DataManager.GetExcelSheet<global::Lumina.Excel.Sheets.TerritoryType>()
                .GetRowOrDefault(agent->SelectedTerritoryId);
            var map = Service.DataManager.GetExcelSheet<global::Lumina.Excel.Sheets.Map>()
                .GetRowOrDefault(agent->SelectedMapId);
            changing |= agent->SelectedTerritoryId == 0 || (!hasMap
                ? territory == null || territory.Value.Map.RowId != 0
                : map is { } selected && selected.Id.ToString().Length > 0);
            contentBounds = null;
            Rendering.UnavailableDrawing.Map(Rendering.UnavailableReason.MapState(false, changing,
                hasMap, hasPath, false));
            return;
        }
        var failed = false;
        contentBounds = System.SystemConfig.IsGratis && System.SystemConfig.FitSmallRoomsToContent
            ? mapContents.Get(AgentMap.Instance()->SelectedMapId,
                $"{AgentMap.Instance()->SelectedMapPath.ToString()}.tex")
            : null;
        if (AgentMap.Instance()->SelectedMapBgPath.Length is 0) {
            var path = $"{agent->SelectedMapPath.ToString()}.tex";
            var shared = Service.TextureProvider.GetFromGame(path);
            if (shared.TryGetWrap(out var texture, out var error)) {
                nativeTextureReady = texture.Handle != default && texture.Width >= 16 && texture.Height >= 16;
                if (nativeTextureReady) DrawColouredMapImage(texture, path, () => LoadMapPixels(path));
            }
            failed = error != null || texture != null && !nativeTextureReady;
        }
        else {
            var foregroundPath = $"{AgentMap.Instance()->SelectedMapPath.ToString()}.tex";
            var backgroundPath = $"{AgentMap.Instance()->SelectedMapBgPath.ToString()}.tex";
            var source = backgroundPath + "|" + foregroundPath;
            if (blendedPath != source) {
                blendedTexture?.Dispose();
                blendedTexture = null;
                // Recorded first: a map that fails to load must not reload, and dispose again, on every frame.
                blendedPath = source;
                // Two map textures are read and multiplied; that is tens of milliseconds, so it is not done on
                // the draw thread. The map stays blank until the pixels are ready, like a recolour that is pending.
                var background = backgroundPath;
                var foreground = foregroundPath;
                blendedWork = Task.Run(() => BlendPixels(background, foreground));
            }

            if (blendedWork is { IsCompleted: true } finished) {
                blendedWork = null;
                if (finished.IsCompletedSuccessfully && finished.Result is { } pixels) {
                    try {
                        blendedTexture = Service.TextureProvider.CreateFromRaw(
                            RawImageSpecification.Rgba32(pixels.Width, pixels.Height), pixels.Rgba);
                    }
                    catch (Exception error) { Service.Log.Warning(error, "Failed to upload map texture"); }
                }
            }

            failed = blendedWork == null && blendedTexture == null;
            if (blendedTexture is not null) {
                nativeTextureReady = blendedTexture.Handle != default && blendedTexture.Width >= 16
                    && blendedTexture.Height >= 16;
                if (nativeTextureReady)
                    DrawColouredMapImage(blendedTexture, source, () => LoadMapPixels(foregroundPath, backgroundPath));
                else
                    failed = true;
            }
        }
        Rendering.UnavailableDrawing.Map(Rendering.UnavailableReason.MapState(nativeTextureReady, changing,
            hasMap, hasPath, failed));
    }

    private void DrawMapImage(IDalamudTextureWrap texture) =>
        frame.DrawMapImage(new Rendering.DrawTexture(texture.Handle, texture.Size));

    private static Rendering.MapLayerPixels? BlendPixels(string vanillaBgPath, string vanillaFgPath) {
        try {
            var bgFile = GetTexFile(vanillaBgPath);
            var fgFile = GetTexFile(vanillaFgPath);

            if (bgFile is null || fgFile is null) {
                Service.Log.Warning("Failed to load map textures");
                return null;
            }

            return Rendering.MapLayerBlend.BlendOrForeground(
                new(bgFile.Header.Width, bgFile.Header.Height, bgFile.GetRgbaImageData()),
                new(fgFile.Header.Width, fgFile.Header.Height, fgFile.GetRgbaImageData()));
        }
        catch (Exception error) {
            Service.Log.Warning(error, "Failed to blend map textures");
            return null;
        }
    }

    private static TexFile? GetTexFile(string rawPath) {
        var path = Service.TextureSubstitutionProvider.GetSubstitutedPath(rawPath);

        if (Path.IsPathRooted(path)) {
            return Service.DataManager.GameData.GetFileFromDisk<TexFile>(path);
        }

        return Service.DataManager.GetFile<TexFile>(path);
    }

}