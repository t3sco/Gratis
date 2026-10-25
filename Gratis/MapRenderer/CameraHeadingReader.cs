using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace Gratis.MapRenderer;

internal enum CameraReadState { Pending, Ready, NotLoggedIn, WrongThread, NoManager, UnsupportedCamera, NoActiveCamera, InvalidAngle, ReadFailed }
internal readonly record struct CameraHeadingSample(float? Yaw, CameraReadState State);

internal sealed class CameraHeadingReader : IDisposable
{
    private readonly object gate = new();
    private CameraHeadingSample sample = new(null, CameraReadState.Pending);
    public CameraHeadingSample Latest { get { lock (gate) return sample; } }
    public CameraHeadingReader() => Service.Framework.Update += Update;
    private static unsafe CameraHeadingSample Read(IFramework framework)
    {
        if (!framework.IsInFrameworkUpdateThread) return new(null, CameraReadState.WrongThread);
        if (!Service.ClientState.IsLoggedIn) return new(null, CameraReadState.NotLoggedIn);
        var manager = CameraManager.Instance();
        if (manager == null) return new(null, CameraReadState.NoManager);
        if (manager->ActiveCameraIndex is not (0 or 2 or 3)) return new(null, CameraReadState.UnsupportedCamera);
        var camera = manager->GetActiveCamera();
        if (camera == null) return new(null, CameraReadState.NoActiveCamera);
        var yaw = camera->DirH;
        return float.IsFinite(yaw) ? new(yaw, CameraReadState.Ready) : new(null, CameraReadState.InvalidAngle);
    }
    private void Update(IFramework framework)
    {
        CameraHeadingSample next;
        try { next = Read(framework); }
        catch (Exception) { next = new(null, CameraReadState.ReadFailed); }
        lock (gate) sample = next;
    }
    public void Dispose() => Service.Framework.Update -= Update;
}
