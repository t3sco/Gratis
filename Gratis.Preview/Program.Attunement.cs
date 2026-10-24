using System.Text.Json;

namespace Gratis.Preview;

internal static partial class Program
{
    private static bool TryAttunementScene(string[] args)
    {
        var index = Array.IndexOf(args, "--scene");
        if (index < 0 || index + 1 >= args.Length || args[index + 1] != "aetheryte-attunement") return false;
        var outputIndex = Array.IndexOf(args, "--out");
        var output = Path.GetFullPath(outputIndex >= 0 ? args[outputIndex + 1] : Path.Combine("_preview", "aetheryte-attunement.png"));
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using var d = new ScriptedFrameDriver(AttunementVerification.Config(), AttunementVerification.Scene());
        AttunementVerification.Fake(d); d.Step(new(0)); d.Step(new(.3));
        var center = d.Renderer.WorldToScreen(new(30, -30), d.Window.MapDrawOffset);
        d.Step(new(.4, Mouse: center)); var frame = d.Step(new(.6)); d.Save(output);
        File.WriteAllText(Path.ChangeExtension(output, ".state.json"), JsonSerializer.Serialize(frame, ScriptedFrameDriver.Json));
        Console.WriteLine($"{output}: Travel lens; one attuned crystal, unattuned crystal/shard; real hover card.");
        return true;
    }
}
