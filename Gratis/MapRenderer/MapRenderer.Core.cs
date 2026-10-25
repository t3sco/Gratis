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

public unsafe partial class MapRenderer : Rendering.IMapRendererHost, IDisposable
{
    public static float Scale
    {
        get => System.SystemConfig.MapScale;
        set => WriteScale(value, new global::System.Diagnostics.StackFrame(1).GetMethod()?.Name ?? "external");
    }

    private readonly Rendering.MapRendererFrame frame;
    public Rendering.MapRendererFrame Frame => frame;
    public Vector2 DrawOffset { get => frame.DrawOffset; set => frame.CenterOnOffset(value); }
    private static void WriteScale(float value, [global::System.Runtime.CompilerServices.CallerMemberName] string writer = "")
    {
        if (System.MapRenderer is { } renderer) {
            renderer.frame.SetViewportScale(value);
            renderer.frame.viewportMotion.Written("scale", writer);
        }
        else System.SystemConfig.MapScale = value;
    }
    public Vector2 DrawPosition => frame.DrawPosition;

    private IDalamudTextureWrap? blendedTexture;
    private string blendedPath = string.Empty;
    private readonly MapContentCache mapContents = new(path => LoadMapPixels(path));
    private Rendering.MapContentBounds? contentBounds;

    public MapRenderer()
    {
        cameraHeading = new CameraHeadingReader();
        frame = new Rendering.MapRendererFrame(this);
        LoadFogHooks();
    }

    public void Dispose()
    {
        UnloadFogHooks();
        cameraHeading.Dispose();
        mapColours.Dispose();
        mapContents.Dispose();
        blendedTexture?.Dispose();
        fogTexture?.Dispose();
    }

    public void CenterOnGameObject(IGameObject obj) => CenterOnCoordinate(new Vector2(obj.Position.X, obj.Position.Z));

    public void CenterOnCoordinate(Vector2 coord) => frame.CenterOnCoordinate(coord);

    private static Vector2 CoordinateOffset(Vector2 coord) => -coord * DrawHelpers.GetMapScaleFactor() + DrawHelpers.GetMapOffsetVector();

    public void DrawBaseTexture() => frame.DrawBaseTexture();
    public void DrawDynamicElements() => frame.DrawDynamicElements();

    Data.SystemConfig Rendering.IMapRendererHost.Config => System.SystemConfig;
    double Rendering.IMapRendererHost.Now => ImGui.GetTime();
    int Rendering.IMapRendererHost.FrameId => ImGui.GetFrameCount();
    uint Rendering.IMapRendererHost.MapId => AgentMap.Instance()->SelectedMapId;
    ulong Rendering.IMapRendererHost.MapKey => ((ulong)AgentMap.Instance()->CurrentTerritoryId << 32) | AgentMap.Instance()->SelectedMapId;
    Rendering.MapContentBounds? Rendering.IMapRendererHost.ContentBounds => contentBounds;
    bool Rendering.IMapRendererHost.SmallMapHint => DrawHelpers.GetMapScaleFactor() >= 2;
    Vector2 Rendering.IMapRendererHost.CoordinateOffset(Vector2 position) => CoordinateOffset(position);
    void Rendering.IMapRendererHost.DrawBackgroundTexture() => DrawBackgroundTexture();
    void Rendering.IMapRendererHost.DrawFogOfWar() => DrawFogOfWar();
    void Rendering.IMapRendererHost.DrawFlag() => DrawFlag();
    void Rendering.IMapRendererHost.DrawPlayer() => DrawPlayer();
    void Rendering.IMapRendererHost.DrawTextMarkers() => DrawStaticTextMarkers();
    void Rendering.IMapRendererHost.CollectMarkers()
    {
        DrawStaticMapMarkers();
        DrawDynamicMarkers();
        DrawGameObjects();
        DrawGroupMembers();
        DrawTemporaryMarkers();
        DrawGatheringMarkers();
        DrawFieldMarkers();
    }

    private void DrawBackgroundTexture()
    {
        contentBounds = System.SystemConfig.IsGratis && System.SystemConfig.FitSmallRoomsToContent
            ? mapContents.Get(AgentMap.Instance()->SelectedMapId, $"{AgentMap.Instance()->SelectedMapPath.ToString()}.tex") : null;
        if (AgentMap.Instance()->SelectedMapBgPath.Length is 0) {
            var texture = Service.TextureProvider.GetFromGame($"{AgentMap.Instance()->SelectedMapPath.ToString()}.tex").GetWrapOrEmpty();

            ImGui.SetCursorPos(DrawPosition);
            var path = $"{AgentMap.Instance()->SelectedMapPath.ToString()}.tex";
            DrawColouredMapImage(texture, path, () => LoadMapPixels(path));
        }
        else {
            var foregroundPath = $"{AgentMap.Instance()->SelectedMapPath.ToString()}.tex";
            var backgroundPath = $"{AgentMap.Instance()->SelectedMapBgPath.ToString()}.tex";
            var source = backgroundPath + "|" + foregroundPath;
            if (blendedPath != source) {
                ResetFogTexture();
                blendedTexture?.Dispose();
                blendedTexture = LoadTexture();
                blendedPath = source;
            }

            if (blendedTexture is not null) {
                ImGui.SetCursorPos(DrawPosition);
                DrawColouredMapImage(blendedTexture, source, () => LoadMapPixels(foregroundPath, backgroundPath));
            }
        }
    }

    private void DrawMapImage(IDalamudTextureWrap texture) => frame.DrawMapImage(new Rendering.DrawTexture(texture.Handle, texture.Size));

    private static IDalamudTextureWrap? LoadTexture()
    {
        var vanillaBgPath = $"{AgentMap.Instance()->SelectedMapBgPath.ToString()}.tex";
        var vanillaFgPath = $"{AgentMap.Instance()->SelectedMapPath.ToString()}.tex";

        var bgFile = GetTexFile(vanillaBgPath);
        var fgFile = GetTexFile(vanillaFgPath);

        if (bgFile is null || fgFile is null) {
            Service.Log.Warning("Failed to load map textures");
            return null;
        }

        var backgroundBytes = bgFile.GetRgbaImageData();
        var foregroundBytes = fgFile.GetRgbaImageData();

        Parallel.For(0, 2048 * 2048, i =>
        {
            var index = i * 4;

            backgroundBytes[index + 0] = (byte)(backgroundBytes[index + 0] * foregroundBytes[index + 0] / 255);
            backgroundBytes[index + 1] = (byte)(backgroundBytes[index + 1] * foregroundBytes[index + 1] / 255);
            backgroundBytes[index + 2] = (byte)(backgroundBytes[index + 2] * foregroundBytes[index + 2] / 255);
        });

        return Service.TextureProvider.CreateFromRaw(RawImageSpecification.Rgba32(2048, 2048), backgroundBytes);
    }

    private static TexFile? GetTexFile(string rawPath)
    {
        var path = Service.TextureSubstitutionProvider.GetSubstitutedPath(rawPath);

        if (Path.IsPathRooted(path)) {
            return Service.DataManager.GameData.GetFileFromDisk<TexFile>(path);
        }

        return Service.DataManager.GetFile<TexFile>(path);
    }

}
