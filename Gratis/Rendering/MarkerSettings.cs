namespace Gratis.Rendering;

public enum MarkerPriority { Objectives, Transport, Ambient }

public sealed class MarkerSettings
{
    public bool Declutter = true;
    public bool ScaleWithZoom = true;
    public float MinimumScale = .7f;
    public float MaximumScale = 1.4f;
    public bool HideLowPriority = true;
    public float ClusterDistance = 24;
    public bool DimIrrelevant = true;
    public float IrrelevantAlpha = .6f;
    public MarkerPriority[] Priority = [MarkerPriority.Objectives, MarkerPriority.Transport, MarkerPriority.Ambient];
}
