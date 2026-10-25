using System.Numerics;
using System.Text.Json;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;

namespace Gratis.Preview;

internal static class PvpVerification {
    private static void Check(bool value, string message) {
        if (!value)
            throw new InvalidOperationException(message);
    }

    public static void Frames() {
        foreach (var style in new[] { 0, 1 }) {
            var config = new SystemConfig {
                VisualStyle = style, AcceptedSpoilerWarning = true, FadeMode = 0,
                MapScale = 1, WindowPosition = new(32), WindowSize = new(640, 480),
                ReduceMotion = true, PvpMapOnly = false,
            };
            config.Markers.Declutter = false;
            using var driver = new ScriptedFrameDriver(config, new FrameScript {
                Markers = [
                new(60444, new(-80, 0), Source: MapMarkerSource.ObjectTable),
                new(60421, new(-40, 0), Source: MapMarkerSource.Party),
                new(60403, new(0, 0), Source: MapMarkerSource.Alliance),
                new(60444, new(40, 0), Source: MapMarkerSource.NativeStatic),
                new(60071, new(80, 0), Objective: true, Source: MapMarkerSource.NativeEvent),
                new(60961, new(0, 40), Source: MapMarkerSource.ObjectTable),
            ]
            });
            driver.Step(new(0));
            driver.Step(new(.1));
            var normal = driver.Trace.DrawnMarkers.Select(m => m.DataId).ToArray();
            Check(normal.Length == 6, "PvP off retains all ordinary marker sources in either style");
            if (style == 0)
                Check(driver.Renderer.BeginSpotlight(new(Vector2.Zero, 1, SpotlightSource.Search)),
                    "Pre-PvP search spotlight");

            driver.Markers[0].PrimaryText = () => throw new InvalidOperationException("ObjectTable enemy label leaked");
            driver.Markers[3].PrimaryText = () =>
                throw new InvalidOperationException("Unverified native enemy label leaked");
            config.DebugMode = true;
            var pvp = driver.Step(new(.2, IsPvP: true));
            Check(pvp.MarkersDrawn == 3, "Only group members and native objective drawn in PvP");
            Check(driver.Trace.DrawnMarkers.All(m =>
                m.Source is MapMarkerSource.Party or MapMarkerSource.Alliance or MapMarkerSource.NativeEvent),
                "No nearby, pet or unverified native player markers");
            Check(!driver.Renderer.Spotlight.Active && config.PvpMapOnly,
                "EnterPvP clears prior spotlight and normalizes disabled saved setting");
            Check(!driver.Renderer.BeginSpotlight(new(Vector2.Zero, 1, SpotlightSource.Search)),
                "PvP search spotlight rejected");
            Check(driver.Context.MarkerTargets.Targets.All(t => !PvpMapPolicy.IsPlayer(t.Marker)),
                "Team markers never feed player nearest/search/snap queries in PvP");
            var policy = driver.Context.Pvp;
            Check(policy.Considered(MapMarkerSource.ObjectTable) == 2
                && policy.Admitted(MapMarkerSource.ObjectTable) == 0 && policy.Drawn(MapMarkerSource.ObjectTable) == 0,
                "ObjectTable considered/rejected/drawn diagnostics");
            Check(policy.Drawn(MapMarkerSource.Party) == 1 && policy.Drawn(MapMarkerSource.Alliance) == 1
                && policy.Drawn(MapMarkerSource.NativeStatic) == 0,
                "Group/native diagnostics");
            Check(policy.Diagnostics().Contains("enemySource=unverified")
                && policy.Diagnostics().Contains("radar=blocked"),
                "Omission reasons are explicit");
            Check(!PvpMapPolicy.Allows(true, new Gratis.Classes.MarkerInfo {
                Position = default, Offset = default, Scale = 1, IconId = 60071
            }), "Unknown provenance is rejected in PvP");
            config.EdgePointerParty = true;
            driver.Context.EdgePointers.ClearPvp();
            driver.Context.EdgePointers.Observe(driver.Markers[1]);
            Check(driver.Context.EdgePointers.Diagnostics().Contains("candidates=0"),
                "Team edge pointers are disabled in PvP too");
            var places = new TravelFixture(driver);
            var ipc = new FakeTravelIpc();
            var assistance = new MapAssistance(ipc.Provider(), places, () => true, () => { });
            assistance.Prepare(config);
            Check(assistance.Request(TravelFixture.Hub).Contains("PvP")
                && assistance.Request(TravelFixture.Shard).Contains("PvP") && ipc.Requests == 0
                && places.Teleports == 0,
                "PvP blocks native teleport and IPC even with a permissive fake travel context");
            var palette = new PaletteFixture(driver).Overlay;
            palette.Open("nearest player");
            Check(!palette.IsOpen, "PvP search never opens or samples its source");

            driver.Markers[0].PrimaryText = null;
            driver.Markers[3].PrimaryText = null;
            config.DebugMode = false;
            driver.Step(new(.3, IsPvP: false));
            Check(driver.Trace.DrawnMarkers.Select(m => m.DataId).SequenceEqual(normal),
                "LeavePvP restores ordinary sources with no stale policy");
            Check(policy.Transitions == 2 && policy.Drawn(MapMarkerSource.ObjectTable) == 2,
                "Transition counts reset per frame");
            driver.Step(new(.4, IsPvP: true));
            Check(driver.Trace.DrawnMarkers.Count == 3 && policy.Transitions == 3,
                "Re-entry filters anew without stale markers");
        }

        using (var driver = new ScriptedFrameDriver(new SystemConfig { AcceptedSpoilerWarning = true, FadeMode = 0 },
            new FrameScript { Markers = [new(60444, Vector2.Zero, Source: MapMarkerSource.ObjectTable)] })) {
            driver.Step(new(0, IsPvP: true));
            Check(driver.Step(new(.1)).MarkersDrawn == 0, "ObjectTable-only PvP enemies produce no marker");
        }
        var defaults = JsonSerializer.Deserialize<SystemConfig>("{}", ScriptedFrameDriver.Json)!;
        Check(defaults.PvpMapOnly, "Missing saved setting defaults on");
        var tweak = TweakCatalog.All.Single(t => t.Id == "pvp-map-only");
        tweak.Set(defaults, false);
        Check(defaults.PvpMapOnly && tweak.Locked, "Tweak cannot disable policy");
        defaults.PvpMapOnly = false;
        SettingsLayout.Reset(defaults, "Map");
        Check(defaults.PvpMapOnly && SettingsLayout.Entries.Any(e => e.Path == "PvpMapOnly" && !e.Enabled(defaults)),
            "Settings reset and disabled UI entry");
        Console.WriteLine("PvP: both styles, source omissions, team/objective draw, lazy labels, "
            + "query/spotlight guards, transitions, diagnostics and mandatory defaults/reset passed.");
    }
}