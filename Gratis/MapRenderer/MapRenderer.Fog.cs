using System;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using Dalamud.Bindings.ImGui;
using Dalamud.Hooking;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Gratis.Compat.Classes;
using TerraFX.Interop.DirectX;

namespace Gratis.MapRenderer;

public unsafe partial class MapRenderer {
    private delegate void ImmediateContextProcessCommands(ImmediateContext* commands,
        RenderCommandBufferGroup* bufferGroup, uint a3);

    [Signature("E8 ?? ?? ?? ?? 48 8B 4B 30 FF 15 ?? ?? ?? ??", DetourName = nameof(OnImmediateContextProcessCommands))]
    private readonly Hook<ImmediateContextProcessCommands>? immediateContextProcessCommandsHook = null;

    private bool requestUpdatedMaskingTexture;
    private readonly object fogGate = new();
    private bool fogDisposed;
    private readonly ConcurrentQueue<(int Generation, MapColourPixels Pixels)> fogUploads = new();
    private MapColourPixels? fogPixels;
    private int fogGeneration, fogFailureLogged;
    private string requestedFogPath = string.Empty, fogSource = string.Empty;
    private IDalamudTextureWrap? fogTexture;
    private int lastKnownDiscoveryFlags;
    private readonly Stopwatch textureLoadStopwatch = new();

    private static int CurrentDiscoveryFlags =>
        AtkStage.Instance()->GetNumberArrayData(NumberArrayType.AreaMap2)->IntArray[2];

    // Fog of war is off by default, so the render hook is only installed the first time it is needed.
    private bool fogHooked;

    private void EnsureFogHooks() {
        if (fogHooked)
            return;
        fogHooked = true;
        try {
            Service.Hooker.InitializeFromAttributes(this);
        }
        catch (Exception exception) {
            Service.Log.Warning(exception, "[Fog of War] Could not install the render hook.");
        }
        if (immediateContextProcessCommandsHook is null)
            Service.Log.Warning("[Fog of War] Render hook not found; fog of war is unavailable this session.");
        immediateContextProcessCommandsHook?.Enable();
        Service.Framework.Update += UploadFogTexture;
    }

    private void UnloadFogHooks() {
        // Say so first, so a call already inside the hook stops queueing work before the hook goes away.
        lock (fogGate) {
            fogDisposed = true;
            Interlocked.Increment(ref fogGeneration);
            fogUploads.Clear();
        }
        Service.Framework.Update -= UploadFogTexture;
        immediateContextProcessCommandsHook?.Dispose();
    }

    private void OnImmediateContextProcessCommands(ImmediateContext* commands, RenderCommandBufferGroup* bufferGroup,
        uint a3) =>

        HookSafety.ExecuteSafe(() => {
            string? path = null;
            var generation = 0;
            lock (fogGate) {
                // The game's mask lags map selection; snapshot request identity before reading it.
                if (!fogDisposed && requestUpdatedMaskingTexture
                    && textureLoadStopwatch is { IsRunning: true, ElapsedMilliseconds: > 200 }) {
                    path = requestedFogPath;
                    generation = fogGeneration;
                    requestUpdatedMaskingTexture = false;
                    textureLoadStopwatch.Stop();
                }
            }
            if (path is not null) {
                var mask = GetPrebakedTextureBytes();
                if (mask is not null)
                    Task.Run(() => LoadFogTexture(path, mask, generation));
            }

            immediateContextProcessCommandsHook!.Original(commands, bufferGroup, a3);
        }, Service.Log, "Exception during OnImmediateContextProcessCommands");

    private void DrawFogOfWar() {
        if (!System.SystemConfig.ShowFogOfWar)
            return;
        EnsureFogHooks();
        if (immediateContextProcessCommandsHook is null)
            return;
        if (CurrentDiscoveryFlags == AgentMap.Instance()->SelectedMapDiscoveryFlag)
            return;
        if (CurrentDiscoveryFlags == -1)
            return;
        if (AgentMap.Instance()->SelectedMapBgPath.Length == 0)
            return;

        var backgroundPath = $"{AgentMap.Instance()->SelectedMapBgPath.ToString()}.tex";
        var source = backgroundPath + "|" + AgentMap.Instance()->SelectedMapPath.ToString();
        lock (fogGate) {
            var flagsChanged = lastKnownDiscoveryFlags != CurrentDiscoveryFlags || fogSource != source;
            lastKnownDiscoveryFlags = CurrentDiscoveryFlags;

            if (flagsChanged) {
                Service.Log.Debug("[Fog of War] Discovery Bits Changed, updating fog texture.");
                requestUpdatedMaskingTexture = true;
                requestedFogPath = backgroundPath;
                fogSource = source;
                Interlocked.Increment(ref fogGeneration);
                textureLoadStopwatch.Restart();
                fogTexture?.Dispose();
                fogTexture = null;
                fogPixels = null;
            }

            if (fogTexture is not null) {
                ImGui.SetCursorPos(DrawPosition);
                var pixels = fogPixels!;
                DrawColouredMapImage(fogTexture, $"fog:{fogSource}:{fogGeneration}", () => pixels);
            }
            else {
                var defaultBackgroundTexture =
                    Service.TextureProvider.GetFromGame(
                        $"{AgentMap.Instance()->SelectedMapBgPath.ToString()}.tex").GetWrapOrEmpty();

                ImGui.SetCursorPos(DrawPosition);
                DrawColouredMapImage(defaultBackgroundTexture, backgroundPath, () => LoadMapPixels(backgroundPath));
            }
        }
    }

    private void ResetFogTexture() {
        lock (fogGate) {
            Interlocked.Increment(ref fogGeneration);
            requestUpdatedMaskingTexture = false;
            fogSource = string.Empty;
            fogTexture?.Dispose();
            fogTexture = null;
            fogPixels = null;
        }
    }

    private void UploadFogTexture(IFramework framework) {
        while (fogUploads.TryDequeue(out var upload)) {
            lock (fogGate) {
                if (fogDisposed || upload.Generation != fogGeneration)
                    continue;
                try {
                    var pixels = upload.Pixels;
                    var texture = Service.TextureProvider.CreateFromRaw(RawImageSpecification.Rgba32(pixels.Width,
                        pixels.Height),
                        pixels.Rgba);
                    fogTexture?.Dispose();
                    fogTexture = texture;
                    fogPixels = pixels;
                }
                catch (Exception error) { LogFogFailure(error); }
            }
        }
    }

    private void LogFogFailure(Exception error) {
        if (Interlocked.Exchange(ref fogFailureLogged, 1) == 0)
            Service.Log.Warning(error, "[Map Colour] Fog preparation failed; using original background.");
    }

    private void LoadFogTexture(string backgroundPath, byte[] maskingTextureBytes, int generation) {
        try {
            if (generation != Volatile.Read(ref fogGeneration))
                return;
            var pixels = LoadMapPixels(backgroundPath);
            if (pixels.Width != 2048 || pixels.Height != 2048 || maskingTextureBytes.Length < 128 * 128 * 4)
                throw new InvalidOperationException("Unexpected map fog dimensions.");
            var backgroundBytes = pixels.Rgba;

            for (var index = 0; index < 2048 * 2048; index++)
                backgroundBytes[index * 4 + 3] = 0;
            for (var x = 0; x < 128; x++)
                for (var y = 0; y < 128; y++) {
                    var pixelIndex = (x + y * 128) * 4;
                    var targetPixel = (x + 2048 * y) * 4;
                    var redAmount = maskingTextureBytes[pixelIndex] / 255.0f;
                    var greenAmount = maskingTextureBytes[pixelIndex + 1] / 255.0f;
                    var blueAmount = maskingTextureBytes[pixelIndex + 2] / 255.0f;
                    var alphaSum = (byte)(Math.Max(redAmount, Math.Max(greenAmount, blueAmount)) * 255);
                    if (alphaSum == 0)
                        continue;
                    const int scaleFactor = 16;
                    for (var xScalar = 0; xScalar < scaleFactor; xScalar++)
                        for (var yScalar = 0; yScalar < scaleFactor; yScalar++) {
                            var target = targetPixel * scaleFactor + xScalar * 4 + yScalar * 2048 * 4;
                            backgroundBytes[target + 3] = alphaSum;
                        }
                }
            CleanupFogTexture(backgroundBytes, generation);
            lock (fogGate) {
                if (!fogDisposed && generation == fogGeneration)
                    fogUploads.Enqueue((generation, pixels));
            }
        }
        catch (Exception error) { LogFogFailure(error); }
    }

    private static byte[]? GetPrebakedTextureBytes() {
        var addon = Service.GameGui.GetAddonByName<AddonAreaMap>("AreaMap");
        if (addon is null)
            return null;

        var componentMap = (void*)Marshal.ReadIntPtr((nint)addon, 0x430);
        if (componentMap is null)
            return null;

        var texturePointer = (Texture*)Marshal.ReadIntPtr((nint)componentMap, 0x270);
        if (texturePointer is null)
            return null;

        var device = (ID3D11Device*)Service.PluginInterface.UiBuilder.DeviceHandle;
        var texture = (ID3D11Texture2D*)texturePointer->D3D11Texture2D;

        D3D11_TEXTURE2D_DESC description;
        texture->GetDesc(&description);

        description.ArraySize = 1;
        description.BindFlags = 0;
        description.MipLevels = 1;
        description.MiscFlags = 0;
        description.CPUAccessFlags = (uint)D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ;
        description.Usage = D3D11_USAGE.D3D11_USAGE_STAGING;

        ID3D11Texture2D* stagingTexture;
        if (device->CreateTexture2D(&description, null, &stagingTexture) < 0)
            return null;

        ID3D11DeviceContext* context;
        device->GetImmediateContext(&context);

        context->CopyResource((ID3D11Resource*)stagingTexture, (ID3D11Resource*)texture);

        D3D11_MAPPED_SUBRESOURCE mapped;
        if (context->Map((ID3D11Resource*)stagingTexture, 0, D3D11_MAP.D3D11_MAP_READ, 0, &mapped) < 0) {
            context->Release();
            stagingTexture->Release();
            return null;
        }

        int bufferSize = (int)(description.Height * mapped.RowPitch);
        byte[] pixelData = new byte[bufferSize];

        Marshal.Copy((IntPtr)mapped.pData, pixelData, 0, bufferSize);

        context->Unmap((ID3D11Resource*)stagingTexture, 0);
        context->Release();
        stagingTexture->Release();

        return pixelData;
    }

    private void CleanupFogTexture(byte[] blockyFogBytes, int generation) {
        var timer = Stopwatch.StartNew();

        const int blurRadius = 8;

        for (var x = 0; x < 2048; x++) {
            if (generation != Volatile.Read(ref fogGeneration))
                return;
            for (var y = 0; y < 2048; y++) {
                var pixelIndex = (x + y * 2048) * 4;

                var alphaAverage = 0.0f;
                var numAveraged = 0;

                if (blockyFogBytes[pixelIndex + 3] == 255)
                    continue;

                for (var xBlur = -blurRadius; xBlur < -blurRadius + blurRadius * 2; ++xBlur) {
                    var currentX = x + xBlur;
                    if (currentX is < 0 or >= 2048)
                        continue;
                    var currentPixelIndex = (currentX + y * 2048) * 4;

                    alphaAverage += blockyFogBytes[currentPixelIndex + 3];
                    numAveraged++;
                }

                for (var yBlur = -blurRadius; yBlur < -blurRadius + blurRadius * 2; ++yBlur) {
                    var currentY = y + yBlur;

                    if (currentY is < 0 or >= 2048)
                        continue;
                    var currentPixelIndex = (x + currentY * 2048) * 4;

                    alphaAverage += blockyFogBytes[currentPixelIndex + 3];
                    numAveraged++;
                }

                var newAlpha = (byte)(alphaAverage / numAveraged);
                blockyFogBytes[pixelIndex + 3] = newAlpha;
            }
        }

        Service.Log.Debug($"Texture Cleanup completed in {timer.ElapsedMilliseconds} ms");
    }
}