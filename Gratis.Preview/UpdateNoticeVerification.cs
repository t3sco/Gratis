using System.Numerics;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class UpdateNoticeVerification {
    private static void Require(bool value, string message) {
        if (!value)
            throw new InvalidOperationException(message);
    }

    public static void Run() {
        var config = ChromeLayoutVerification.Config(new(780, 600));
        Require(config.ShowUpdateNotice && config.DismissedUpdate.Length == 0, "The update notice is on by default");
        var opened = 0;
        UpdateNotice.OpenInstaller = () => opened++;
        UpdateNotice.Available = "";
        using var driver = new ScriptedFrameDriver(config, new FrameScript { Width = 1000, Height = 800 });
        try {
            double t = 0;
            void Step(Vector2? mouse = null, bool? left = null) =>
                driver.Step(new(t += .05, Mouse: mouse ?? new(-1000), Left: left));
            void Click(Vector2 at) {
                Step(at);
                Step(at, true);
                Step(at, false);
                Step(at);
            }
            Step();
            Step();
            Require(UpdateNotice.LastBounds == Vector4.Zero, "No notice appears when there is no update");
            UpdateNotice.Available = "0.1.0.4";
            Step();
            Step();
            var bounds = UpdateNotice.LastBounds;
            Require(bounds.Z > bounds.X && bounds.X > 390 && bounds.Y < 120, "The notice sits in the top-right corner");
            Click(UpdateNotice.ActionCenter);
            Require(opened == 1, "The Update button opens the plugin installer");
            Click(UpdateNotice.DismissCenter);
            Step();
            Require(config.DismissedUpdate == "0.1.0.4" && UpdateNotice.LastBounds == Vector4.Zero,
                "Dismissing hides the notice for that version");
            UpdateNotice.Available = "0.1.0.4";
            Step();
            Step();
            Require(UpdateNotice.LastBounds != Vector4.Zero, "A newer version shows the notice again");
            config.ShowUpdateNotice = false;
            Step();
            Require(UpdateNotice.LastBounds == Vector4.Zero, "The setting turns the notice off");
        }
        finally {
            UpdateNotice.Available = "";
            UpdateNotice.OpenInstaller = null;
        }
    }
}
