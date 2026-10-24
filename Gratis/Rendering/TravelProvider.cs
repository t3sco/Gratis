using System;

namespace Gratis.Rendering;

public readonly record struct TravelDestination(uint Id, string Name, string Route, bool Attuned,
    bool Aethernet, uint? Cost = null);

public interface ITravelProvider
{
    bool Available { get; }
    string Version { get; }
    bool IsBusy { get; }
    uint ActiveAetheryte { get; }
    string LastAction { get; }
    bool Travel(TravelDestination destination);
    void Abort();
}

public sealed class NullTravelProvider : ITravelProvider
{
    public static readonly NullTravelProvider Instance = new();
    public bool Available => false;
    public string Version => "absent";
    public bool IsBusy => false;
    public uint ActiveAetheryte => 0;
    public string LastAction => "none";
    public bool Travel(TravelDestination destination) => false;
    public void Abort() { }
}

public sealed class LifestreamTravelProvider(Func<string?> detect, Func<bool> busy,
    Func<uint> active, Func<uint, bool> aethernet, Func<uint, byte, bool> teleport,
    Action abort) : ITravelProvider
{
    private bool failed;
    private string? detectedVersion;
    public bool Available {
        get {
            try {
                var current = detect();
                if (current != detectedVersion) { failed = false; detectedVersion = current; }
                if (current == null || failed) return false;
                _ = busy();
                return true;
            }
            catch { failed = true; return false; }
        }
    }
    public string Version => detectedVersion ?? "absent";
    public string LastAction { get; private set; } = "none";
    public bool IsBusy { get { try { return Available && busy(); } catch { failed = true; return false; } } }
    public uint ActiveAetheryte { get { try { return Available ? active() : 0; } catch { failed = true; return 0; } } }
    public bool Travel(TravelDestination destination)
    {
        if (!destination.Attuned || destination.Id == 0 || !Available || IsBusy) return false;
        try {
            var accepted = destination.Aethernet ? aethernet(destination.Id) : teleport(destination.Id, 0);
            LastAction = (accepted ? "requested: " : "refused: ") + destination.Route;
            return accepted;
        }
        catch { failed = true; LastAction = "IPC unavailable"; return false; }
    }
    public void Abort()
    {
        if (!Available) return;
        try { abort(); LastAction = "cancelled"; }
        catch { failed = true; LastAction = "cancel unavailable"; }
    }
}
