using System.Numerics;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class LabelLayoutVerification {
    private static void Require(bool value, string message) {
        if (!value)
            throw new InvalidOperationException(message);
    }

    public static void Run() {
        var config = ChromeLayoutVerification.Config(new(780, 600));
        Require(config.HideOverlappingLabels, "Overlapping labels are hidden by default");
        using var driver = new ScriptedFrameDriver(config, new FrameScript { Width = 1000, Height = 800 });
        driver.Step(new(.04));
        Require(LabelLayout.TryPlace(new(100, 100), new(300, 140)), "The first label is placed");
        Require(!LabelLayout.TryPlace(new(250, 120), new(450, 160)), "A label on top of another is skipped");
        Require(!LabelLayout.TryPlace(new(302, 100), new(400, 140)), "A label touching another is skipped");
        Require(LabelLayout.TryPlace(new(100, 200), new(300, 240)), "A clear label is placed");
        driver.Step(new(.08));
        Require(LabelLayout.TryPlace(new(250, 120), new(450, 160)), "Each frame starts with a clean slate");
    }
}
