using System.Text.Json;
using System.Text.Json.Nodes;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Compat.Configuration;

namespace Gratis.Preview;

internal static class ConfigVerification {
    private static readonly JsonSerializerOptions Json = Configuration.SerializerOptions;

    public static void Run() {
        const string source = """
            { "TitleCorner": 2, "TitleOffset": { "X": 21, "Y": 18 }, "ConeSize": 83.75,
              "MapGlow": true, "MapVignette": 0.46, "FollowOnOpen": true, "Version": 1,
              "CharacterName": "Legacy Fixture", "CharacterWorld": "Test World",
              "ContentId": 1234567890123456789, "LodestoneId": "7654321" }
            """;
        var old = JsonNode.Parse(source)!.AsObject();
        old["FlatVignette"] = .27f;
        old["UnknownFormerSetting"] = true;
        var config = JsonSerializer.Deserialize<SystemConfig>(old.ToJsonString(), Json)!;
        var saved = JsonSerializer.SerializeToNode(config, Json)!.AsObject();
        var compared = 0;
        foreach (var field in typeof(SystemConfig).GetFields()) {
            if (!old.TryGetPropertyValue(field.Name, out var original)
                || !saved.TryGetPropertyValue(field.Name, out var restored))
                continue;
            if (!JsonNode.DeepEquals(original, restored))
                throw new InvalidOperationException($"Legacy value changed: {field.Name}.");
            compared++;
        }
        foreach (var name in new[] { "Version", "CharacterName", "CharacterWorld", "ContentId", "LodestoneId" }) {
            if (!JsonNode.DeepEquals(old[name], saved[name]))
                throw new InvalidOperationException($"Legacy metadata changed: {name}.");
        }
        if (saved.ContainsKey("$type") || saved.ContainsKey("ProfilePicture")
            || saved.ContainsKey("PurgeProfilePicture"))
            throw new InvalidOperationException("Type names or ignored portrait state were written to config.");
        var defaults = JsonSerializer.Deserialize<SystemConfig>("{}", Json)!;
        if (defaults.Version != 2 || defaults.CharacterName != "Unknown Name"
            || defaults.CharacterWorld != "Unknown World" || defaults.ContentId != 0
            || defaults.LodestoneId is not null)
            throw new InvalidOperationException("Missing legacy metadata defaults changed.");
        config.LodestoneId = null;
        if (JsonSerializer.SerializeToNode(config, Json)!["LodestoneId"] is not null)
            throw new InvalidOperationException("Null legacy LodestoneId changed.");
        if (saved.ContainsKey("FlatVignette") || saved.ContainsKey("UnknownFormerSetting"))
            throw new InvalidOperationException("Removed fields were written back.");
        if (config.FramePreset != FramePreset.Hairline || !config.ScaleWithFrame || !config.ClampToMap)
            throw new InvalidOperationException("Missing fields did not retain defaults.");
        var roundTrip = JsonSerializer.Deserialize<SystemConfig>(saved.ToJsonString(), Json)!;
        if (!JsonNode.DeepEquals(saved, JsonSerializer.SerializeToNode(roundTrip, Json)))
            throw new InvalidOperationException("Config round trip changed values.");
        config.VisualStyle = 1;
        if (config.IsGratis)
            throw new InvalidOperationException("Classic selection failed.");
        config.VisualStyle = 0;
        if (!config.IsGratis)
            throw new InvalidOperationException("Gratis selection failed.");
        Console.WriteLine($"Config verified: embedded legacy fixture; {compared} saved fields and five "
            + "metadata properties unchanged, no type names/portrait state, defaults and round trip preserved.");
    }
}