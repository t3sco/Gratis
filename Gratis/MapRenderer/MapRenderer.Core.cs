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

public unsafe partial class MapRenderer : IDisposable
{
    public static float Scale
    {
        get => System.SystemConfig.MapScale;
        set => System.SystemConfig.MapScale = value;
    }

    public Vector2 DrawOffset { get; set; }
    public Vector2 DrawPosition { get; private set; }

    private IDalamudTextureWrap? blendedTexture;
    private string blendedPath = string.Empty;

    public MapRenderer()
    {
        LoadFogHooks();
    }

    public void Dispose()
    {
        UnloadFogHooks();
        mapColours.Dispose();
        blendedTexture?.Dispose();
        fogTexture?.Dispose();
    }

    public void CenterOnGameObject(IGameObject obj) => CenterOnCoordinate(new Vector2(obj.Position.X, obj.Position.Z));

    public void CenterOnCoordinate(Vector2 coord)
    {
        followZoomOffset = Vector2.Zero;
        DrawOffset = CoordinateOffset(coord);
    }

    private static Vector2 CoordinateOffset(Vector2 coord) => -coord * DrawHelpers.GetMapScaleFactor() + DrawHelpers.GetMapOffsetVector();

    public void DrawBaseTexture()
    {
        viewportSize = ImGui.GetContentRegionAvail();
        DrawBackgroundTexture();
    }

    public void DrawDynamicElements()
    {
        DrawFogOfWar();
        DrawMapMarkers();
    }

    private void UpdateDrawOffset()
    {
        DrawPosition = Rendering.ViewportMath.DrawPosition(viewportSize, DrawOffset, Scale);
    }

    private void DrawBackgroundTexture()
    {
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

    private void DrawMapImage(IDalamudTextureWrap texture)
    {
        PrepareViewport(texture.Size);
        ImGui.SetCursorPos(DrawPosition);
        Rendering.MapTextureDrawing.DrawMapImage(new Rendering.DrawTexture(texture.Handle, texture.Size), Scale);
    }

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

    private void DrawMapMarkers()
    {
        DrawStaticMapMarkers();
        DrawDynamicMarkers();
        DrawGameObjects();
        DrawGroupMembers();
        DrawTemporaryMarkers();
        DrawGatheringMarkers();
        DrawFieldMarkers();
        DrawPlayer();
        DrawStaticTextMarkers();
        DrawFlag();
    }
}
