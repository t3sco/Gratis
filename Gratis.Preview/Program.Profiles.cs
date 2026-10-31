using Gratis.Data;
using Gratis.Windows;

namespace Gratis.Preview;

internal static partial class Program {
    private static bool TryProfilesScene(string[] args) {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || args[at + 1] != "profiles")
            return false;
        var translated = args.ToArray();
        translated[at + 1] = "settings-appearance";
        var options = Options.Parse(translated)!;
        RenderSettings(options with {
            Scene = "profiles",
            Output = Array.IndexOf(args, "--out") < 0 ? Path.GetFullPath("_preview/profiles.png") : options.Output
        });
        return true;
    }

    internal static SystemConfig ProfilesSceneConfig() {
        var config = new SystemConfig();
        ProfileManager.Save(config, "Default look");
        config.IconScale = .8f;
        config.MapCircle = true;
        ProfileManager.Save(config, "Night circle with a long name");
        config.ZoomSpeed = .2f;
        ProfileManager.Save(config, "Quiet");
        ProfileManager.Apply(config, 1);
        return config;
    }
}
