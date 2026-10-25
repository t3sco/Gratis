using System.Text.Json;
using Gratis.Classes;
using Gratis.Controllers;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;
using ImGuiNET;

namespace Gratis.Preview;

internal static class AttunementVerification {
    private static void Check(bool value, string name) {
        if (!value)
            throw new InvalidOperationException("Attunement: " + name);
    }

    private static MarkerInfo Marker(MarkerIconFamily family, bool? attuned) => new() {
        Position = default,
        Offset = default,
        Scale = 1,
        IconId = 60453,
        IconFamily = family,
        AetheryteAttuned = attuned,
    };
    public static void State() {
        var places = PlaceIndexBuilder.Build([new(1, 10, "City", "Region"), new(2, 10, "City upper", "Region")],
            [new(100, 1, 10, "Hub", true, 7, AethernetNameId: 500),
                new(101, 2, 10, "Shard", false, 7, AethernetNameId: 501)]).ToArray();
        Check(AetheryteAttunement.Find(places, MarkerIconFamily.Aetheryte, 100, 1)!.AetheryteId == 100,
            "main crystal DataKey is Aetheryte ID");
        Check(AetheryteAttunement.Find(places, MarkerIconFamily.Aethernet, 501, 2)!.AetheryteId == 101,
            "shard maps AethernetName to its own unlock ID");
        Check(AetheryteAttunement.Find(places, MarkerIconFamily.Aethernet, 501, 2)!.HubId == 100,
            "shard unlock independent of hub");
        Check(AetheryteAttunement.Find(places, MarkerIconFamily.Aethernet, 101, 2) is null, "wrong key domain unknown");
        Check(AetheryteAttunement.Find(places, MarkerIconFamily.Aethernet, 501, 99)!.AetheryteId == 101,
            "unique cross-layer fallback");
        Check(AetheryteAttunement.Find(places, MarkerIconFamily.Aetheryte, 0, 1) is null, "zero key");
        Check(AetheryteAttunement.Find(places, MarkerIconFamily.Exit, 100, 1) is null, "unrelated type");
        var shard = places.Single(p => p.AetheryteId == 101);
        var duplicate = places.Append(shard with { MapId = 3, AetheryteId = 102 });
        Check(AetheryteAttunement.Find(duplicate, MarkerIconFamily.Aethernet, 501, 99) is null,
            "ambiguous key stays unknown");
        Check(AetheryteAttunement.Find(duplicate, MarkerIconFamily.Aethernet, 501, 2)!.AetheryteId == 101,
            "map disambiguates");
        var c = JsonSerializer.Deserialize<SystemConfig>("{}", ScriptedFrameDriver.Json)!;
        Check(c.AetheryteAttunementStyle, "old-config default on");
        var marker = Marker(MarkerIconFamily.Aetheryte, false);
        Check(AetheryteAttunement.Status(c, marker) == false && AetheryteAttunement.Opacity(c, marker, false) == .55f,
            "unattuned dim");
        Check(AetheryteAttunement.Opacity(c, marker, true) == .72f, "travel loud remains prominent");
        Check(MarkerVisibility.IsPriority(marker), "unattuned remains protected");
        marker.AetheryteAttuned = true;
        Check(AetheryteAttunement.Opacity(c, marker, false) == 1, "attuned normal");
        marker.AetheryteAttuned = null;
        Check(AetheryteAttunement.Opacity(c, marker, false) == 1, "unknown normal");
        marker.AetheryteAttuned = false;
        marker.IconFamily = MarkerIconFamily.Aethernet;
        Check(AetheryteAttunement.Status(c, marker) == false, "shard styling");
        marker.IsSpotlighted = true;
        Check(AetheryteAttunement.Opacity(c, marker, false) == 1, "Spotlight target full alpha");
        marker.IsSpotlighted = false;
        c.AetheryteAttunementStyle = false;
        Check(AetheryteAttunement.Status(c, marker) is null, "option off");
        SettingsLayout.Entries.Single(e => e.Path == "AetheryteAttunementStyle").Reset(c, new());
        Check(c.AetheryteAttunementStyle, "reset");
        c.VisualStyle = 1;
        Check(AetheryteAttunement.Status(c, marker) is null && AetheryteAttunement.Opacity(c, marker, false) == 1,
            "Classic unchanged");
    }

    internal static SystemConfig Config() => new() {
        AcceptedSpoilerWarning = true, FadeMode = 0, ReduceMotion = true,
        FollowPlayer = false,
        PinChrome = true,
        ActiveLens = "Travel", LensAuto = false,
        WindowPosition = new(32),
        WindowSize = new(640, 480)
    };
    internal static FrameScript Scene() =>
        new() { Markers = [new(60453, new(-130, -30)), new(60453, new(30, -30)), new(60430, new(160, 60))] };
    internal static void Fake(ScriptedFrameDriver d) {
        d.Markers[0].AetheryteAttuned = true;
        d.Markers[0].PrimaryText = () => "Attuned crystal";
        d.Markers[1].AetheryteAttuned = false;
        d.Markers[1].PrimaryText = () => "Unattuned crystal";
        d.Markers[2].AetheryteAttuned = false;
        d.Markers[2].PrimaryText = () => "Unattuned aethernet shard";
    }

    public static void Frames() {
        using var d = new ScriptedFrameDriver(Config(), Scene());
        Fake(d);
        d.Step(new(0));
        var normal = d.Step(new(.3));
        var on = ImGui.GetDrawData().TotalVtxCount;
        Check(normal.MarkersDrawn == 3 && d.Context.Trace!.DrawnMarkers.All(MarkerVisibility.IsPriority),
            "all crystals/shards remain drawn");
        Check(AetheryteAttunementDrawing.Opacity(d.Markers[1]) == .72f, "Travel lens shared presentation");
        d.Config.AetheryteAttunementStyle = false;
        d.Step(new(.4));
        var off = ImGui.GetDrawData().TotalVtxCount;
        Check(on > off, "enabled style submits dashed ring geometry");
        d.Config.AetheryteAttunementStyle = true;
        var center = d.Renderer.WorldToScreen(new(30, -30), d.Window.MapDrawOffset);
        d.Step(new(.5, Mouse: center));
        d.Step(new(.7));
        Check(d.Context.HoverCardFrame == d.FrameId && d.Context.GetIconFont(12) is not null,
            "shared hover card and Lucide atlas");
        foreach (var lens in Lenses.BuiltIn) {
            d.Config.ActiveLens = lens.Name;
            var frame = d.Step(new(.8 + Array.IndexOf(Lenses.BuiltIn, lens) * .1, Mouse: new(-1000)));
            Check(frame.MarkersDrawn == 3, "priority markers survive lens " + lens.Name);
        }
        d.Config.VisualStyle = 1;
        Check(d.Step(new(2)).MarkersDrawn == 3, "Classic still draws all markers");
    }
}