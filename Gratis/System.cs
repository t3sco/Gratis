using System.Collections.Generic;
using Dalamud.Interface.ManagedFontAtlas;
using Gratis.Compat.Classes;
using Gratis.Compat.CommandManager;
using Gratis.Compat.Window;
using Gratis.Classes.Caches;
using Gratis.Controllers;
using Gratis.Data;
using Gratis.Modules;
using Gratis.Windows;

namespace Gratis;

#pragma warning disable CS8618
public static class System {
    public static SystemConfig SystemConfig { get; set; }
    public static IconConfig IconConfig { get; set; }
    public static FlagConfig FlagConfig { get; set; }
    public static WindowManager WindowManager { get; set; }
    public static MapWindow MapWindow { get; set; }
    public static ConfigurationWindow ConfigWindow { get; set; }
    public static MapRenderer.MapRenderer MapRenderer { get; set; }
    public static IntegrationsController IntegrationsController { get; set; }
    public static ZoneSwitchController? ZoneSwitchController { get; set; }
    public static AddonAreaMapController AreaMapController { get; set; }
    public static FlagController FlagController { get; set; }
    public static CommandManager CommandManager { get; set; }
    public static Teleporter Teleporter { get; set; }

    public static List<ModuleBase> Modules { get; set; } = [
        new TripleTriadModule(),
        new FateModule(),
        new StellarModule(),
        new PublicContent()
    ];

    public static TooltipCache TooltipCache { get; set; } = new();
    public static CardRewardCache CardRewardCache { get; set; } = new();
    public static GatheringPointNameCache GatheringPointNameCache { get; set; } = new();
    public static GatheringPointIconCache GatheringPointIconCache { get; set; } = new();
    public static TripleTriadCache TripleTriadCache { get; set; } = new();
    public static AetheryteAethernetCache AetheryteAethernetCache { get; set; } = new();

    public static IFontHandle LargeAxisFontHandle { get; set; }
}
