namespace Gratis.Rendering;

public partial class MapDrawingConfig {
    public InterfaceAnimationMode InterfaceAnimations = InterfaceAnimationMode.Full;
    public float AnimationSpeed = 1;
    public InterfaceAnimationMode EffectiveInterfaceMotion => !IsGratis
        || InterfaceAnimations == InterfaceAnimationMode.Off
        ? InterfaceAnimationMode.Off : ReduceMotion ? InterfaceAnimationMode.Reduced : InterfaceAnimations;
}
