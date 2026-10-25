namespace Gratis.Rendering;

public enum MarkerIconStyle {
    Icons, Shapes, Game
}

public sealed class MarkerSettings {
    public MarkerIconStyle IconStyle = MarkerIconStyle.Game;
    public bool Declutter = true;
    public bool ScaleWithZoom = true;
    public float MinimumScale = 1;
    public float MaximumScale = 1;
    public float ClusterDistance = 11;
    public float Opacity = 1;
    public bool DimIrrelevant = true;
    public float IrrelevantAlpha = 0;
}