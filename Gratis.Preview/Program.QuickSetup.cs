using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static partial class Program {
    private static bool TryQuickSetupScene(string[] args) {
        var index = Array.IndexOf(args, "--scene");
        if (index < 0 || index + 1 >= args.Length || args[index + 1] != "quick-setup")
            return false;
        var sizeIndex = Array.IndexOf(args, "--size");
        var size = sizeIndex >= 0 ? args[sizeIndex + 1].Split('x') : ["960", "740"];
        var width = int.Parse(size[0]);
        var height = int.Parse(size[1]);
        var outIndex = Array.IndexOf(args, "--out");
        var output = Path.GetFullPath(outIndex >= 0 ? args[outIndex + 1] : "_preview/quick-setup.png");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var variantIndex = Array.IndexOf(args, "--variant");
        var variant = variantIndex >= 0 ? args[variantIndex + 1] : "welcome";
        var config = QuickSetupVerification.Fresh(width, height);
        using var driver = new ScriptedFrameDriver(config, new FrameScript {
            Width = width, Height = height, Title = "Yok Tural - Tuliyollal",
            Markers = [new(60412, new Vector2(-80, -100)), new(60434, new Vector2(130, 100)),
                new(60450, new Vector2(-270, -140)), new(61000, new Vector2(240, -60))],
        });
        var setup = driver.Context.QuickSetup;
        var time = 0.0;
        void Idle(int frames) {
            for (var i = 0; i < frames; i++)
                driver.Step(new(time += .04, Mouse: new(-1000), Left: false));
        }
        void Click(string label) {
            var b = setup.BoundsOf(label);
            var at = new Vector2((b.X + b.Z) / 2, (b.Y + b.W) / 2);
            driver.Step(new(time += .04, Mouse: at, Left: false));
            driver.Step(new(time += .04, Mouse: at, Left: true));
            driver.Step(new(time += .04, Mouse: at, Left: false));
            Idle(3);
        }
        Idle(8);
        if (variant != "welcome") {
            Click("Next");
            if (variant == "classic-look")
                Click("Classic");
            else if (variant != "look")
                Click("Next");
            if (variant == "done")
                Click("Next");
        }
        Idle(8);
        driver.Save(output);
        Console.WriteLine($"Wrote {output}");
        return true;
    }
}
