namespace Gratis.Rendering;

internal sealed class NativeMapSound {
    private bool owned;
    private (short Id, bool Disabled) original;

    public (short Id, bool Disabled) Apply(bool suppress, short id, bool disabled) {
        if (suppress) {
            if (!owned) {
                original = (id, disabled);
                owned = true;
            }
            return (0, true);
        }
        if (!owned)
            return (id, disabled);
        owned = false;
        return original;
    }

    // A new addon instance must never inherit the previous instance's saved flags.
    public void Forget() => owned = false;
}
