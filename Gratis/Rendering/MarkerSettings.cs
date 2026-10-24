namespace Gratis.Rendering;

public enum MarkerIconStyle { Icons, Shapes, Game }

public sealed class MarkerSettings
{
    public MarkerIconStyle IconStyle = MarkerIconStyle.Icons;
    public bool Declutter = false;
    public bool ScaleWithZoom = false;
    public float MinimumScale = .7f;
    public float MaximumScale = 1.4f;
    public float ClusterDistance = 24;
    public bool DimIrrelevant = false;
    public float IrrelevantAlpha = .6f;
}
