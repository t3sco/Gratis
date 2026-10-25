namespace Gratis.Preview;

internal static partial class Program {
    private static bool TryWindowPresetsScene(string[] args) {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || args[at + 1] != "window-presets")
            return false;
        var translated = args.ToArray();
        translated[at + 1] = "settings-map";
        var options = Options.Parse(translated)!;
        RenderSettings(options with {
            Scene = "window-presets",
            Output = Array.IndexOf(args, "--out") < 0 ? Path.GetFullPath("_preview/window-presets.png") : options.Output
        });
        return true;
    }
}