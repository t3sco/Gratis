using System.Text.Json;

namespace Gratis.Preview;

internal static partial class Program {
    private static bool TryPlaceChipScene(string[] args) {
        var index = Array.IndexOf(args, "--scene");
        if (index < 0 || index + 1 >= args.Length || args[index + 1] is not ("subzone-banner" or "eorzea-time"))
            return false;
        var scene = args[index + 1];
        var outputIndex = Array.IndexOf(args, "--out");
        var output = Path.GetFullPath(outputIndex >= 0 ? args[outputIndex + 1]
            : Path.Combine("_preview", scene + ".png"));
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var config = PlaceChipVerification.Config();
        config.ShowEorzeaTime = scene == "eorzea-time";
        config.PinChrome = scene == "eorzea-time";
        using var d = new ScriptedFrameDriver(config,
            new FrameScript { SubZone = PlaceChipVerification.Location(), Title = "La Noscea - Lower La Noscea" });
        d.Step(new(0, Focused: false));
        d.Step(new(2));
        d.Step(new(2.1, SubZone: scene == "subzone-banner" ? PlaceChipVerification.Location(3) : null));
        var frame = d.Step(new(2.3));
        d.Save(output);
        File.WriteAllText(Path.ChangeExtension(output, ".state.json"),
            JsonSerializer.Serialize(frame, ScriptedFrameDriver.Json));
        Console.WriteLine($"{output}: banner alpha={frame.SubZoneOpacity:0.00}; {frame.EorzeaText}");
        return true;
    }
}