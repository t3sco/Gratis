using Dalamud.IoC;
using Dalamud.Plugin.Services;

namespace KamiLib.Classes;
#pragma warning disable CS8618
internal class LogWrapper {
    [PluginService] public IPluginLog Log { get; set; }
}