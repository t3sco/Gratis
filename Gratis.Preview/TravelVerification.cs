using System.Numerics;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class TravelVerification
{
    internal static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public static void Provider()
    {
        var ipc = new FakeTravelIpc(); var provider = ipc.Provider();
        var destination = new TravelDestination(10, "Aftcastle", "Lower Decks > Aftcastle", true, true);
        Require(provider.Available && !provider.IsBusy && provider.ActiveAetheryte == 1, "detected IPC state");
        Require(!provider.Travel(destination with { Attuned = false }) && ipc.Requests == 0, "unattuned blocked");
        Require(provider.Travel(destination) && provider.IsBusy && ipc.Requests == 1, "hop accepted");
        Require(!provider.Travel(destination) && ipc.Requests == 1, "busy blocks a second request");
        provider.Abort(); Require(!provider.IsBusy && ipc.Cancels == 1, "cancel invokes abort");
        Require(provider.Travel(destination with { Aethernet = false }), "destination IPC");
        ipc.Failing = true;
        Require(!provider.Available && !provider.IsBusy && !provider.Travel(destination), "failure hides and refuses");
        provider.Abort(); Require(ipc.Cancels == 1, "failed cancel is silent");
        ipc.Version = null; Require(!provider.Available && provider.Version == "absent", "unloaded");
        ipc.Failing = false; ipc.Busy = false; ipc.Version = "2.6.0.8";
        Require(provider.Available, "reload recovers");
        Require(!NullTravelProvider.Instance.Available && !NullTravelProvider.Instance.Travel(destination), "null provider inert");
        NullTravelProvider.Instance.Abort();
    }

    public static void Frames()
    {
        var config = PaletteVerification.Config();
        using var driver = new ScriptedFrameDriver(config, new FrameScript());
        var ipc = new FakeTravelIpc(); var source = new TravelFixture(driver);
        var ui = driver.Context.Assistance = new MapAssistance(ipc.Provider(), source, () => true, () => { });
        driver.Step(new(0)); driver.Step(new(.02));
        Require(config.TravelActions && config.TravelActionsInitialized, "default on after detection");
        ui.HoverPlace(TravelFixture.Shard, new(250, 220));
        driver.Step(new(.04)); driver.Step(new(.06));
        Require(ui.CardMax.X > ui.CardMin.X && ui.CardMax.Y > ui.CardMin.Y, "interactive hover card");
        driver.Step(new(.08, Mouse: ui.TravelButtonCenter));
        driver.Step(new(.10, Left: true)); driver.Step(new(.12, Left: false));
        Require(ipc.Requests == 1 && ipc.Busy, "hover card button requests travel");
        driver.Step(new(.13));
        driver.Step(new(.14, Mouse: ui.CancelButtonCenter));
        driver.Step(new(.16, Left: true)); driver.Step(new(.18, Left: false));
        Require(ipc.Cancels == 1 && !ipc.Busy, "progress button cancels travel");
        config.TravelConfirmGil = 100;
        Require(ui.Request(TravelFixture.Hub).StartsWith("Confirm "), "gil confirmation");
        Require(ipc.Requests == 1, "confirmation does not execute");
        Require(ui.Request(TravelFixture.Hub, true) == "Travel requested." && ipc.Requests == 2, "confirmed cost");
        ipc.Version = null; driver.Step(new(.20));
        Require(!ui.CanOffer(TravelFixture.Hub), "missing provider hides actions");
    }
}
