using System.Numerics;
using System.Text.Json;
using Gratis.Controllers;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;

namespace Gratis.Preview;

internal static class ZoneSwitchVerification {
    public static void Run() {
        var options = new ZoneSwitchOptions(true, true, true);
        var request = new ZoneSwitchRequest(100, 200, 10, 100, 10, 0);
        var input = new ZoneSwitchInput(200, 200, 20, 100, 10, true, false, false, false, true, double.PositiveInfinity,
            0);
        (string Name, ZoneSwitchOptions Options, ZoneSwitchRequest Request, ZoneSwitchInput Input,
            ZoneSwitchDecision Expected)[] cases = [
            ("teleport", options, request, input, ZoneSwitchDecision.Switch),
            ("zone line", options, request, input, ZoneSwitchDecision.Switch),
            ("duty entry", options, request, input with { InDuty = true }, ZoneSwitchDecision.Switch),
            ("duty exit", options, request with { OldTerritoryId = 800, SelectedTerritoryId = 800 },
                input with { SelectedTerritoryId = 800 }, ZoneSwitchDecision.Switch),
            ("housing entry", options, request with { NewTerritoryId = 900 },
                input with { TerritoryId = 900, CurrentTerritoryId = 900 }, ZoneSwitchDecision.Switch),
            ("login stale selection", options, request with { OldTerritoryId = 0, OldMapId = 0 }, input,
                ZoneSwitchDecision.Switch),
            ("login empty selection", options,
                request with { OldMapId = 0, SelectedMapId = 0, SelectedTerritoryId = 0 },
                input with { SelectedMapId = 0, SelectedTerritoryId = 0 }, ZoneSwitchDecision.Switch),
            ("reload current map", options,
                request with { OldTerritoryId = 200, OldMapId = 20, SelectedTerritoryId = 200, SelectedMapId = 20 },
                input with { SelectedTerritoryId = 200, SelectedMapId = 20 }, ZoneSwitchDecision.Switch),
            ("reload browsing", options, request with { OldTerritoryId = 200, OldMapId = 20 }, input,
                ZoneSwitchDecision.BrowsingOtherMap),
            ("browse another map", options, request with { SelectedMapId = 99 }, input with { SelectedMapId = 99 },
                ZoneSwitchDecision.BrowsingOtherMap),
            ("same map id different territory", options, request with { SelectedTerritoryId = 999 },
                input with { SelectedTerritoryId = 999 }, ZoneSwitchDecision.BrowsingOtherMap),
            ("browsing override", options with { OnlyIfViewingCurrentZone = false },
                request with { SelectedMapId = 99 }, input with { SelectedMapId = 99 }, ZoneSwitchDecision.Switch),
            ("master disabled", options with { AutoSwitchOnZoneChange = false }, request, input,
                ZoneSwitchDecision.Disabled),
            ("duty disabled", options with { SwitchInDuties = false }, request, input with { InDuty = true },
                ZoneSwitchDecision.Duty),
            ("duty disabled outside", options with { SwitchInDuties = false }, request, input,
                ZoneSwitchDecision.Switch),
            ("closed selection only", options, request, input with { WindowOpen = false }, ZoneSwitchDecision.Switch),
            ("loading", options, request, input with { Loading = true }, ZoneSwitchDecision.Wait),
            ("null player", options, request, input with { PlayerReady = false }, ZoneSwitchDecision.Wait),
            ("null agent", options, request, input with { CurrentTerritoryId = 0, CurrentMapId = 0 },
                ZoneSwitchDecision.Wait),
            ("zero current map", options, request, input with { CurrentMapId = 0 }, ZoneSwitchDecision.Wait),
            ("agent still old territory", options, request, input with { CurrentTerritoryId = 100 },
                ZoneSwitchDecision.Wait),
            ("client still old territory", options, request, input with { TerritoryId = 100 }, ZoneSwitchDecision.Wait),
            ("zero client territory", options, request, input with { TerritoryId = 0 }, ZoneSwitchDecision.Wait),
            ("recent quest link", options, request, input with { UserActionAgeSeconds = .2 },
                ZoneSwitchDecision.UserAction),
            ("one second boundary", options, request, input with { UserActionAgeSeconds = 1 },
                ZoneSwitchDecision.UserAction),
            ("expired user action", options, request, input with { UserActionAgeSeconds = 1.001 },
                ZoneSwitchDecision.Switch),
            ("link during retry stays protected", options, request,
                input with { UserActionAgeSeconds = 4, UserActionVersion = 1 }, ZoneSwitchDecision.UserAction),
            ("search changed selection", options, request, input with { SelectedMapId = 99 },
                ZoneSwitchDecision.UserAction),
            ("native already selected destination", options, request,
                input with { SelectedTerritoryId = 200, SelectedMapId = 20 }, ZoneSwitchDecision.Switch),
            ("PvP", options, request, input with { InPvP = true }, ZoneSwitchDecision.PvP),
            ("hook recursion guard", options, request, input with { Applying = true }, ZoneSwitchDecision.Reentrant),
            ("layer within territory", options, request with { OldTerritoryId = 200, SelectedTerritoryId = 200 },
                input with { SelectedTerritoryId = 200 }, ZoneSwitchDecision.Switch),
            ("browsing another layer", options,
                request with { OldTerritoryId = 200, SelectedTerritoryId = 200, SelectedMapId = 11 },
                input with { SelectedTerritoryId = 200, SelectedMapId = 11 }, ZoneSwitchDecision.BrowsingOtherMap),
            ("empty selected territory", options, request with { SelectedTerritoryId = 0 },
                input with { SelectedTerritoryId = 0 }, ZoneSwitchDecision.Switch),
            ("instance entry shared map", options, request, input with { CurrentMapId = 10, InDuty = true },
                ZoneSwitchDecision.Switch),
            ("instance exit shared map", options, request, input with { CurrentMapId = 10 }, ZoneSwitchDecision.Switch),
            ("override still respects links", options with { OnlyIfViewingCurrentZone = false }, request,
                input with { UserActionAgeSeconds = .1 }, ZoneSwitchDecision.UserAction),
        ];
        var checks = 0;
        void Check(bool ok, string name) {
            if (!ok)
                throw new InvalidOperationException("Zone switch: " + name);
            checks++;
        }
        foreach (var test in cases) {
            var actual = ZoneSwitchPolicy.Decide(test.Options, test.Request, test.Input);
            Check(actual == test.Expected, $"{test.Name}: expected {test.Expected}, got {actual}");
        }

        var attempt = new ZoneSwitchAttempt();
        Check(!attempt.TryBeginApply(), "no pending request cannot apply");
        attempt.Request(request);
        attempt.Request(request with { OldTerritoryId = 200, NewTerritoryId = 300, OldMapId = 20 });
        Check(attempt.Pending is { OldTerritoryId: 100, NewTerritoryId: 300, OldMapId: 10, SelectedMapId: 10 },
            "rapid double zone change retains original viewing intent and latest destination");
        var latest = input with { TerritoryId = 300, CurrentTerritoryId = 300, CurrentMapId = 30 };
        Check(ZoneSwitchPolicy.Decide(options, attempt.Pending!.Value, input) == ZoneSwitchDecision.Wait,
            "intermediate zone can never consume latest request");
        Check(ZoneSwitchPolicy.Decide(options, attempt.Pending.Value, latest) == ZoneSwitchDecision.Switch,
            "rapid double zone change switches directly to final map");
        Check(ZoneSwitchPolicy.Decide(options, attempt.Pending.Value,
            latest with { UserActionVersion = 1 }) == ZoneSwitchDecision.UserAction,
            "superseding an event does not discard a user link during the first retry");
        Check(attempt.TryBeginApply() && !attempt.TryBeginApply(), "nested automatic switch rejected");
        Check(ZoneSwitchPolicy.Decide(options, attempt.Pending.Value,
            latest with { Applying = attempt.IsApplying }) == ZoneSwitchDecision.Reentrant,
            "native OpenMap callback cannot re-enter policy");
        attempt.EndApply();
        attempt.Complete();
        Check(!attempt.IsApplying && attempt.Pending is null && !attempt.TryBeginApply(),
            "completed switch cannot repeat");
        attempt.Request(request with {
            OldTerritoryId = 300,
            NewTerritoryId = 100,
            OldMapId = 30,
            SelectedTerritoryId = 300,
            SelectedMapId = 30
        });
        Check(attempt.Pending is { OldMapId: 30, SelectedMapId: 30 }, "next zone starts with fresh viewing intent");

        var json = new JsonSerializerOptions { IncludeFields = true };
        var old = JsonSerializer.Deserialize<SystemConfig>("{\"FollowOnOpen\":false,\"MapScale\":1.7}", json)!;
        Check(old.AutoSwitchOnZoneChange && old.OnlyIfViewingCurrentZone && old.SwitchInDuties && old.MapScale == 1.7f,
            "old settings preserve saved values and default all new switches ON");
        old.AutoSwitchOnZoneChange = old.OnlyIfViewingCurrentZone = old.SwitchInDuties = false;
        var restored = JsonSerializer.Deserialize<SystemConfig>(JsonSerializer.Serialize(old, json), json)!;
        Check(!restored.AutoSwitchOnZoneChange && !restored.OnlyIfViewingCurrentZone && !restored.SwitchInDuties,
            "explicit OFF values survive save/reload");
        var paths = new[] { "AutoSwitchOnZoneChange", "OnlyIfViewingCurrentZone", "SwitchInDuties" };
        Check(paths.All(path => SettingsLayout.Entries.Any(entry => entry.Path == path)),
            "all options have settings controls");

        foreach (var returning in new[] { false, true }) {
            var viewport = new ViewportTimeline();
            viewport.Config.FollowReturnToPlayer = returning;
            viewport.Tick(0);
            viewport.Drag(.1, new Vector2(90, -30));
            viewport.Wheel(2);
            viewport.Motion.FollowZoomOffset = new Vector2(7, 11);
            viewport.Config.FollowPlayer = true;
            viewport.Motion.Follow.Reset();
            viewport.Motion.Cancel();
            viewport.Motion.FollowZoomOffset = Vector2.Zero;
            viewport.Player = Vector2.One;
            viewport.Target = new Vector2(-120, 60);
            viewport.Offset = viewport.Target;
            viewport.Map = 2;
            viewport.Held = viewport.Hovered = viewport.Focused = false;
            viewport.Tick(.2);
            Check(viewport.Offset == viewport.Target && viewport.Motion.PendingZoom is null
                && viewport.Motion.FollowZoomOffset == Vector2.Zero
                && viewport.Motion.Follow.State == FollowState.Following,
                "zone snaps to new player without stale zoom or detached follow (return=" + returning + ")");
        }
        Console.WriteLine($"Zone selftest: {cases.Length} decision cases, {checks} checks passed. "
            + "Game-only: native AgentMap selection/texture refresh, loading and duty timing, "
            + "closed-window visibility, live hooks and zone fade/slide.");
    }
}