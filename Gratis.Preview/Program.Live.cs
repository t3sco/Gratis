using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Gratis.Data;

namespace Gratis.Preview;

internal static partial class Program {
    private static bool TryDiff(string[] args) {
        if (args.Length == 0 || args[0] != "--diff")
            return false;
        if (args is not ["--diff", var ours, var reference, "--out", var output])
            throw new ArgumentException("Usage: --diff ours.png reference.png --out diff.png");
        ImageDiff.Run(ours, reference, output);
        return true;
    }

    private static bool TryLiveScene(string[] args) {
        if (!args.Where((value, index) => value == "--scene" && index + 1 < args.Length
            && args[index + 1] == "live").Any())
            return false;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2) {
            if (index + 1 == args.Length)
                throw new ArgumentException($"Missing value for {args[index]}.");
            if (args[index] is not (
                "--scene" or "--config" or "--state" or "--map-id" or "--map" or "--out" or "--size" or "--window-size"
                    or "--position" or "--at"))
                throw new ArgumentException($"Unknown live option {args[index]}.");
            if (!values.TryAdd(args[index], args[index + 1]))
                throw new ArgumentException($"Repeated option {args[index]}.");
        }
        var script = values.TryGetValue("--state", out var statePath)
            ? JsonSerializer.Deserialize<FrameScript>(File.ReadAllText(statePath), ScriptedFrameDriver.Json)
                ?? throw new ArgumentException("Empty state.")
            : new FrameScript();
        var configPath = values.GetValueOrDefault("--config") ?? script.Config
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher",
                "pluginConfigs", "Gratis", "System.config.json");
        if (values.ContainsKey("--state") && !values.ContainsKey("--config") && script.Config is not null)
            configPath = Path.GetFullPath(configPath, Path.GetDirectoryName(Path.GetFullPath(statePath!))!);
        configPath = ResolveLiveConfig(configPath);
        var bytes = File.ReadAllBytes(configPath);
        var config = JsonSerializer.Deserialize<SystemConfig>(bytes, ScriptedFrameDriver.Json)
            ?? throw new ArgumentException("Empty config.");
        if (!config.AcceptedSpoilerWarning)
            throw new ArgumentException("Live config has not accepted the spoiler warning; use a config saved after "
                + "accepting it in-game.");
        if (values.TryGetValue("--window-size", out var windowSize))
            config.WindowSize = ParsePair(windowSize, 'x');
        if (values.TryGetValue("--position", out var position))
            config.WindowPosition = ParsePair(position, ',');
        if (values.TryGetValue("--map-id", out var mapId))
            script.MapId = uint.Parse(mapId);
        if (values.TryGetValue("--map", out var map))
            script.Texture = Path.GetFullPath(map);
        else if (script.Texture is not null && statePath is not null)
            script.Texture = Path.GetFullPath(script.Texture, Path.GetDirectoryName(Path.GetFullPath(statePath))!);
        if (values.TryGetValue("--size", out var size)) {
            var display = ParsePair(size, 'x');
            script.Width = (int)display.X;
            script.Height = (int)display.Y;
        }
        else if (statePath is null) {
            script.Width = (int)MathF.Ceiling(MathF.Max(960, config.WindowPosition.X + config.WindowSize.X + 32));
            script.Height = (int)MathF.Ceiling(MathF.Max(640, config.WindowPosition.Y + config.WindowSize.Y + 32));
        }
        var output = Path.GetFullPath(values.GetValueOrDefault("--out") ?? Path.Combine("_preview", "live.png"));
        if (!Path.GetExtension(output).Equals(".png", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("--out must be a PNG.");
        var snapshotPath = Path.ChangeExtension(output, ".state.json");
        if (configPath.Equals(output, StringComparison.OrdinalIgnoreCase)
            || configPath.Equals(snapshotPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Live outputs must differ from the config path.");
        var at = values.TryGetValue("--at", out var timestamp) ? double.Parse(timestamp)
            : script.Frames.LastOrDefault()?.Time ?? .2;
        if (!double.IsFinite(at) || at < 0)
            throw new ArgumentException("--at must be finite and nonnegative.");
        using var driver = new ScriptedFrameDriver(config, script);
        if (script.Frames.Count == 0) {
            for (var index = 0; index < 12; index++)
                driver.Step(new(at * index / 12));
        }
        else
            foreach (var frame in script.Frames) {
                if (frame.Time > at)
                    break;
                driver.Step(frame);
            }
        var snapshot = driver.Step(new(at));
        driver.Save(output);
        File.WriteAllText(snapshotPath, JsonSerializer.Serialize(new {
            Config = configPath,
            ConfigSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            MapId = snapshot.MapId,
            script.TerritoryId,
            script.Texture,
            script.Width,
            script.Height,
            config.WindowPosition,
            config.WindowSize,
            Snapshot = snapshot,
        }, ScriptedFrameDriver.Json));
        Console.WriteLine($"Live state: map {snapshot.MapId}, window {config.WindowSize}, read-only config "
            + $"{configPath}; {snapshotPath}");
        if (script.Texture is null)
            Console.WriteLine("Terrain is procedural; --map must supply the matching full game-map PNG for "
                + "pixel comparison.");
        return true;
    }

    private static string ResolveLiveConfig(string path) {
        path = Path.GetFullPath(path);
        if (Directory.Exists(path))
            path = Path.Combine(path, "System.config.json");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                "No saved Gratis config found; pass --config <System.config.json> or its directory.",
                path);
        return path;
    }

    private static Vector2 ParsePair(string value, char separator) {
        var parts = value.Split(separator);
        if (parts.Length != 2 || !float.TryParse(parts[0], out var x) || !float.TryParse(parts[1], out var y)
            || !float.IsFinite(x) || !float.IsFinite(y))
            throw new ArgumentException($"Expected two finite values separated by '{separator}'.");
        return new Vector2(x, y);
    }

    private static void VerifyLiveScene() {
        var directory = Path.Combine(AppContext.BaseDirectory, "live-selftest");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "System.config.json");
        var output = Path.Combine(directory, "live.png");
        const string original = """
            { "AcceptedSpoilerWarning": true, "MapScale": 0.75, "FadeMode": 0,
              "WindowPosition": {"X": 16, "Y": 16}, "WindowSize": {"X": 256, "Y": 192},
              "UnknownSavedField": "preserve on disk" }
            """;
        File.WriteAllText(path, original);
        TryLiveScene(["--scene", "live", "--config", directory, "--map-id", "134", "--size", "320x240", "--at", ".04",
            "--out", output]);
        if (File.ReadAllText(path) != original)
            throw new InvalidOperationException("Live rendering modified the saved config.");
        using var json = JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(output, ".state.json")));
        var root = json.RootElement;
        if (root.GetProperty("MapId").GetUInt32() != 134
            || root.GetProperty("WindowSize").GetProperty("X").GetSingle() != 256
            || root.GetProperty("Snapshot").GetProperty("Scale").GetSingle() != .75f
            || root.GetProperty("ConfigSha256").GetString() != Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(path))))
            throw new InvalidOperationException(
                "Live output did not preserve the requested map, window, scale and config fingerprint.");
    }
}