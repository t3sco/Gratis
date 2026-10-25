using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Gratis.MapRenderer;

internal enum CameraReadState {
    Pending,
    Ready,
    NotLoggedIn,
    WrongThread,
    NoManager,
    UnsupportedCamera,
    NoActiveCamera,
    InvalidAngle,
    ReadFailed,
    NoMap,
    NoConeArray
}
internal readonly record struct CameraHeadingSample(float? Yaw, CameraReadState State, float? NativeConeDegrees = null,
    CameraReadState ConeState = CameraReadState.Pending);

internal sealed class CameraHeadingReader : IDisposable {
    private readonly object gate = new();
    private CameraHeadingSample sample = new(null, CameraReadState.Pending);
    public CameraHeadingSample Latest {
        get {
            lock (gate)
                return sample;
        }
    }

    public CameraHeadingReader() => Service.Framework.Update += Update;
    private static unsafe CameraHeadingSample ReadManager() {
        var manager = CameraManager.Instance();
        if (manager == null)
            return new(null, CameraReadState.NoManager);
        if (manager->ActiveCameraIndex is not (0 or 2 or 3))
            return new(null, CameraReadState.UnsupportedCamera);
        var camera = manager->GetActiveCamera();
        if (camera == null)
            return new(null, CameraReadState.NoActiveCamera);
        var yaw = camera->DirH;
        return float.IsFinite(yaw) ? new(yaw, CameraReadState.Ready) : new(null, CameraReadState.InvalidAngle);
    }

    private static unsafe (float? Degrees, CameraReadState State) ReadCone() {
        var agent = AgentMap.Instance();
        if (agent == null || agent->CurrentMapId == 0)
            return (null, CameraReadState.NoMap);
        if (AtkStage.Instance() == null)
            return (null, CameraReadState.NoConeArray);
        var array = AreaMapNumberArray.Instance();
        if (array == null)
            return (null, CameraReadState.NoConeArray);
        var degrees = array->ConeRotation;
        return degrees is >= -3600 and <= 3600 ? (degrees, CameraReadState.Ready)
            : (null, CameraReadState.InvalidAngle);
    }

    private void Update(IFramework framework) {
        var guard = !framework.IsInFrameworkUpdateThread ? CameraReadState.WrongThread : !Service.ClientState.IsLoggedIn
            ? CameraReadState.NotLoggedIn : CameraReadState.Ready;
        if (guard != CameraReadState.Ready) {
            lock (gate)
                sample = new(null, guard, null, guard);
            return;
        }
        CameraHeadingSample next;
        try {
            next = ReadManager();
        }
        catch (Exception) { next = new(null, CameraReadState.ReadFailed); }
        try {
            var cone = ReadCone();
            next = next with { NativeConeDegrees = cone.Degrees, ConeState = cone.State };
        }
        catch (Exception) { next = next with { ConeState = CameraReadState.ReadFailed }; }
        lock (gate)
            sample = next;
    }

    public void Dispose() => Service.Framework.Update -= Update;
}