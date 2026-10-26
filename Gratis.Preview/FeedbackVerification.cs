using System.Numerics;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class FeedbackVerification {
    private static void Require(bool value, string message) {
        if (!value)
            throw new InvalidOperationException(message);
    }

    private static void Wait(Func<bool> done) {
        for (var i = 0; i < 100 && !done(); i++)
            Thread.Sleep(20);
    }

    public static void Run() {
        Button();
        Sending();
    }

    private static void Button() {
        var config = ChromeLayoutVerification.Config(new(780, 600));
        Require(config.ShowFeedbackButton, "The feedback button is on by default");
        using var driver = new ScriptedFrameDriver(config, new FrameScript { Width = 1000, Height = 800 });
        double time = 0;
        void Step(Vector2? mouse = null, bool? left = null) =>
            driver.Step(new(time += .04, Mouse: mouse ?? new(-1000), Left: left));
        Step();
        Step();
        var bounds = FeedbackButtonDrawing.LastBounds;
        Require(bounds.Z > bounds.X && bounds.X < 100 && bounds.W > 400, "The feedback button sits bottom-left");
        var center = ChromePolishVerification.Center(bounds);
        Step(center);
        Step(center, true);
        Step(center, false);
        Step(center);
        Step(center);
        Require(FeedbackButtonDrawing.PopupOpen, "Clicking the button opens the feedback form");
        driver.Save(Path.Combine(AppContext.BaseDirectory, "feedback-popup.png"));
        driver.Step(new(time += .04, Escape: true));
        Step();
        Step();
        Require(!FeedbackButtonDrawing.PopupOpen, "Escape closes the feedback form");
        config.ShowFeedbackButton = false;
        Step();
        Require(FeedbackButtonDrawing.LastBounds == Vector4.Zero, "The setting hides the feedback button");
    }

    private static void Sending() {
        var original = FeedbackSender.Post;
        try {
            FeedbackSender.Post = _ => throw new HttpRequestException("offline");
            FeedbackSender.Send("hello", null);
            Wait(() => FeedbackSender.State != FeedbackState.Sending);
            Require(FeedbackSender.State == FeedbackState.Failed && FeedbackSender.Error.Length > 0,
                "A failed send reports a plain-language error");
            Require(FeedbackSender.CanSend, "A failed send can be retried straight away");

            string? body = null;
            FeedbackSender.Post = json => {
                body = json;
                return Task.FromResult(true);
            };
            FeedbackSender.Send("   ", null);
            Require(FeedbackSender.State == FeedbackState.Failed && body is null, "An empty message is never sent");
            FeedbackSender.Send("the map is great", "# report");
            Wait(() => FeedbackSender.State == FeedbackState.Sent);
            Require(FeedbackSender.State == FeedbackState.Sent && body is not null
                && body.Contains("the map is great") && body.Contains("# report") && body.Contains("version"),
                "A send carries the message, report and version");
            Require(!FeedbackSender.CanSend, "A second send right away is held back");
            body = null;
            FeedbackSender.Send("again", null);
            Require(body is null, "The cooldown stops rapid repeat sends");

            FeedbackSender.Reset();
            Require(FeedbackSender.State == FeedbackState.Idle, "Reopening the form clears the last result");
        }
        finally { FeedbackSender.Post = original; }
    }
}
