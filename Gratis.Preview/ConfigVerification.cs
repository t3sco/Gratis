using System.Text.Json;
using System.Text.Json.Nodes;
using Gratis.Data;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class ConfigVerification
{
    private static readonly JsonSerializerOptions Json = new() { IncludeFields = true, WriteIndented = true };

    public static void Run()
    {
        var legacyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIVLauncher", "pluginConfigs", "MappyGlass", "System.config.json");
        var source = File.Exists(legacyPath) ? File.ReadAllText(legacyPath) : """
            { "TitleCorner": 2, "TitleOffset": { "X": 21, "Y": 18 }, "ConeSize": 83.75,
              "MapGlow": true, "MapVignette": 0.46, "FollowOnOpen": true, "Version": 2 }
            """;
        var old = JsonNode.Parse(source)!.AsObject();
        old["FlatVignette"] = .27f;
        old["UnknownFormerSetting"] = true;
        var config = JsonSerializer.Deserialize<SystemConfig>(old.ToJsonString(), Json)!;
        var saved = JsonSerializer.SerializeToNode(config, Json)!.AsObject();
        var compared = 0;
        foreach (var field in typeof(SystemConfig).GetFields()) {
            if (!old.TryGetPropertyValue(field.Name, out var original) ||
                !saved.TryGetPropertyValue(field.Name, out var restored)) continue;
            if (!JsonNode.DeepEquals(original, restored))
                throw new InvalidOperationException($"Legacy value changed: {field.Name}.");
            compared++;
        }
        if (saved.ContainsKey("FlatVignette") || saved.ContainsKey("UnknownFormerSetting"))
            throw new InvalidOperationException("Removed fields were written back.");
        if (config.FramePreset != FramePreset.Hairline || !config.ScaleWithFrame || !config.ClampToMap)
            throw new InvalidOperationException("Missing fields did not retain defaults.");
        var roundTrip = JsonSerializer.Deserialize<SystemConfig>(saved.ToJsonString(), Json)!;
        if (!JsonNode.DeepEquals(saved, JsonSerializer.SerializeToNode(roundTrip, Json)))
            throw new InvalidOperationException("Config round trip changed values.");
        config.VisualStyle = 1;
        if (config.IsGratis) throw new InvalidOperationException("Classic selection failed.");
        config.VisualStyle = 0;
        if (!config.IsGratis) throw new InvalidOperationException("Gratis selection failed.");
        Console.WriteLine($"Config verified: {(File.Exists(legacyPath) ? "existing MappyGlass config (read-only)" : "synthetic legacy config")}; " +
                          $"{compared} saved fields unchanged, removed/unknown fields ignored, defaults and round trip preserved.");
    }
}
