using System;
using System.Text;
using Gratis.Classes;

namespace Gratis.Rendering;

public enum MapMarkerSource {
    Unspecified,
    ObjectTable,
    Party,
    Alliance,
    NativeStatic,
    NativeEvent,
    NativeTemporary,
    NativeFlag,
    NativeWaymark
}

public sealed class PvpMapPolicy {
    private readonly int[] considered = new int[Enum.GetValues<MapMarkerSource>().Length];
    private readonly int[] admitted = new int[Enum.GetValues<MapMarkerSource>().Length];
    private readonly int[] drawn = new int[Enum.GetValues<MapMarkerSource>().Length];
    private int frame = -1;
    public bool IsPvP { get; private set; }
    public int Transitions { get; private set; }
    public int Considered(MapMarkerSource source) => considered[(int)source];
    public int Admitted(MapMarkerSource source) => admitted[(int)source];
    public int Drawn(MapMarkerSource source) => drawn[(int)source];

    public bool Synchronize(bool isPvP) {
        if (IsPvP == isPvP)
            return false;
        IsPvP = isPvP;
        Transitions++;
        frame = -1;
        ClearCounts();
        return true;
    }

    public void BeginFrame(bool isPvP, int frameId) {
        Synchronize(isPvP);
        if (frame == frameId)
            return;
        frame = frameId;
        ClearCounts();
    }

    private void ClearCounts() {
        Array.Clear(considered);
        Array.Clear(admitted);
        Array.Clear(drawn);
    }

    public static bool IsPlayer(MarkerInfo marker) => marker.Source is MapMarkerSource.Party or MapMarkerSource.Alliance
        || marker.IconId is 60403 or 60421 or 60443 or 60444
        || MarkerIcon.Family(marker) is MarkerIconFamily.Player or MarkerIconFamily.Party or MarkerIconFamily.Alliance;

    public static bool Allows(bool isPvP, MarkerInfo marker) {
        if (!isPvP)
            return true;
        if (marker.Source == MapMarkerSource.ObjectTable)
            return false;
        if (marker.Source is MapMarkerSource.Party or MapMarkerSource.Alliance)
            return true;
        if (IsPlayer(marker) || MarkerIcon.Family(marker) == MarkerIconFamily.Pet)
            return false;
        return marker.Source is MapMarkerSource.NativeStatic or MapMarkerSource.NativeEvent or MapMarkerSource.NativeTemporary or MapMarkerSource.NativeFlag or MapMarkerSource.NativeWaymark;
    }

    public bool Consider(MarkerInfo marker) {
        considered[(int)marker.Source]++;
        if (!Allows(IsPvP, marker))
            return false;
        admitted[(int)marker.Source]++;
        return true;
    }

    public void RecordDrawn(MarkerInfo marker) => drawn[(int)marker.Source]++;

    public string Diagnostics() {
        var result = new StringBuilder($"IsPvP={IsPvP} mandatory=true enemySource=unverified frame={frame} "
            + $"transitions={Transitions}; counts considered/admitted/drawn (last rendered "
            + "frame; drawn after culling): ");
        foreach (var source in Enum.GetValues<MapMarkerSource>())
            result.Append($"{source}={Considered(source)}/{Admitted(source)}/{Drawn(source)}; ");
        result.Append(IsPvP ? "omissions: ObjectTable/nearby/hostile/NPC/pet/distance sources and "
            + "radar=blocked before enumeration (population not sampled); non-team players=no "
            + "verified spotted feed; player hover/search/nearest/edge pointers=blocked; "
            + "search spotlight=blocked; teleport/Lifestream=blocked; group=party/alliance "
            + "only, no distance cutoff; native non-player markers=retained; live window=native map fallback"
            : "omissions: PvP policy inactive; ordinary style, icon settings and viewport/lens culling apply");
        return result.ToString();
    }
}