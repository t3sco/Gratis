using System.Collections.Generic;
using Gratis.Compat.Configuration;

namespace Gratis.Data;

public class IconConfig
{
    public Dictionary<uint, IconSetting> IconSettingMap = [];

    public static IconConfig Load() => Service.PluginInterface.LoadConfigFile<IconConfig>("Icons.config.json");

    public void Save() => Service.PluginInterface.SaveConfigFile("Icons.config.json", System.IconConfig);
}