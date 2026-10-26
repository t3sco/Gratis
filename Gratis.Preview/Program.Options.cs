using Gratis.Rendering;

namespace Gratis.Preview;

internal static partial class Program {
    private sealed record Options(string Scene, string Output, int Width, int Height, string? Map, float? FeatherWidth,
        bool Verify, bool ViewportOff, FramePreset? Frame = null, bool? Circle = null, float Dpi = 1,
        MarkerIconStyle IconStyle = MarkerIconStyle.Icons, bool? Declutter = null, int? Style = null) {
        public static Options? Parse(string[] args) {
            var scene = "map-default";
            string? output = null, map = null;
            float? featherWidth = null;
            var verify = false;
            var viewportOff = false;
            var width = 1280;
            var height = 800;
            var dpi = 1f;
            var iconStyle = MarkerIconStyle.Icons;
            bool? declutter = null;
            int? style = null;
            for (var i = 0; i < args.Length; i++) {
                if (args[i] == "--verify") {
                    verify = true;
                    continue;
                }
                if (args[i] == "--viewport-off") {
                    viewportOff = true;
                    continue;
                }
                if (args[i] is "--help" or "-h") {
                    Console.WriteLine("Frame commands: --script timeline.json [--out directory]; --scene live "
                        + "[--config path] [--map-id id] [--window-size WxH] [--size WxH] [--position X,Y] "
                        + "[--state scene.json] [--map full-map.png] [--at seconds] [--out live.png]; "
                        + "--diff ours.png reference.png --out diff.png; --selftest all|frames|diff.");
                    var sceneNames = string.Join('|',
                        Scenes.Concat(ViewportScenes).Concat(FrameScenes).Concat(MarkerScenes).Concat(
                            MotionScenes).Concat(FollowScenes).Append("player-heading"));
                    Console.WriteLine($"dotnet run --project Gratis.Preview -- --scene <{sceneNames}> "
                        + "[--out path.png] [--size WxH] [--dpi 1|2] [--icons Icons|Shapes|Game] "
                        + "[--declutter on|off] [--map image.png] [--feather-width 0..48] [--verify] "
                        + "[--viewport-off]; --selftest "
                        + "heading|viewport|motion|motion-timeline|follow|config|settings|chrome|markers|marker-classification");
                    return null;
                }
                if (i + 1 >= args.Length)
                    throw new ArgumentException($"Missing value for {args[i]}.");
                var key = args[i++];
                var value = args[i];
                switch (key) {
                    case "--style":
                        style = value switch { "gratis" => 0, "classic" => 1,
                            _ => throw new ArgumentException("Style must be gratis or classic.") };
                        break;
                    case "--scene":
                        scene = value;
                        break;
                    case "--out":
                        output = value;
                        break;
                    case "--map":
                        map = value;
                        break;
                    case "--dpi":
                        if (!float.TryParse(value, out dpi) || dpi is not (1 or 2))
                            throw new ArgumentException("DPI must be 1 or 2.");
                        break;
                    case "--icons":
                        if (!Enum.TryParse(value, true, out iconStyle) || !Enum.IsDefined(iconStyle))
                            throw new ArgumentException("Icons must be Icons, Shapes or Game.");
                        break;
                    case "--declutter":
                        declutter = value.ToLowerInvariant() switch {
                            "on" => true,
                            "off" => false,
                            _ => throw new ArgumentException("Declutter must be on or off.")
                        };
                        break;
                    case "--feather-width":
                        if (!float.TryParse(value, out var feather) || !float.IsFinite(feather) || feather < 0
                            || feather > 48)
                            throw new ArgumentException("Feather width must be 0..48.");
                        featherWidth = feather;
                        break;
                    case "--size": {
                            var parts = value.Split('x', 'X');
                            if (parts.Length != 2 || !int.TryParse(parts[0], out width)
                                || !int.TryParse(parts[1], out height))
                                throw new ArgumentException("Size must be WxH, for example 1280x800.");
                            break;
                        }
                    default:
                        throw new ArgumentException($"Unknown option {key}.");
                }
            }
            if (!IsKnownScene(scene))
                throw new ArgumentException($"Unknown scene '{scene}'. Expected "
                    + $"{string.Join(", ", Scenes.Concat(ViewportScenes).Concat(FrameScenes))}.");
            if (width < 320 || height < 240 || width > 8192 || height > 8192 || (long)width * height > 16777216)
                throw new ArgumentException("Size must be 320x240 to 8192x8192, with at most 16 megapixels.");
            if (map is not null && (!File.Exists(map)
                || !Path.GetExtension(map).Equals(".png", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("--map must name an existing PNG.");
            if (output is not null && !Path.GetExtension(output).Equals(".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("--out must have a .png extension.");
            output ??= Path.Combine(ProjectRoot(), "_preview", scene + ".png");
            return new Options(scene, output, width, height, map, featherWidth, verify, viewportOff, Dpi: dpi,
                IconStyle: iconStyle, Declutter: declutter, Style: style);
        }

        private static bool IsKnownScene(string scene) => IsLensScene(scene)
            || scene is "player-heading" or "player-heading-frame" or "tweaks-page" or "tweaks-page-search"
                or "tweaks-whatsnew"
            || FollowScenes.Contains(scene) || Scenes.Contains(scene) || IsViewportScene(scene)
            || FrameScenes.Contains(scene) || IsMarkerScene(scene) || IsMotionScene(scene);

        internal static string ProjectRoot() {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Gratis.sln")))
                    return dir.FullName;
            return Directory.GetCurrentDirectory();
        }
    }
}
