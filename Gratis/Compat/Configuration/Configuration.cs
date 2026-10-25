/*
    Copyright (C) 2022  MidoriKami

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.IO;
using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Utility;
using Gratis.Compat.Classes;

namespace Gratis.Compat.Configuration;

public static partial class Configuration {
    public static T LoadConfigFile<T>(this IDalamudPluginInterface pluginInterface, string fileName) where T : new()
        => LoadFile<T>(pluginInterface, Path.Combine(pluginInterface.ConfigDirectory.FullName, fileName));

    public static void SaveConfigFile<T>(this IDalamudPluginInterface pluginInterface, string fileName, T file)
        => SaveFile(pluginInterface, Path.Combine(pluginInterface.GetPluginConfigDirectory(), fileName), file);

    private static T LoadFile<T>(IDalamudPluginInterface pluginInterface, string filePath) where T : new() {
        var fileInfo = new FileInfo(filePath);
        if (fileInfo is { Exists: true }) {
            try {
                var fileText = File.ReadAllText(fileInfo.FullName);
                var dataObject = JsonSerializer.Deserialize<T>(fileText, SerializerOptions);

                if (dataObject is null) {
                    dataObject = new T();
                    SaveFile(pluginInterface, filePath, dataObject);
                }

                return dataObject;
            }
            catch (Exception e) {
                var localLog = pluginInterface.Create<LogWrapper>();
                localLog?.Log.Error(e, $"Error trying to load file {filePath}, creating a new one instead.");

                SaveFile(pluginInterface, filePath, new T());
            }
        }

        var newFile = new T();
        SaveFile(pluginInterface, filePath, newFile);

        return newFile;
    }

    private static void SaveFile<T>(IDalamudPluginInterface pluginInterface, string filePath, T file) {
        try {
            var fileText = JsonSerializer.Serialize(file, file!.GetType(), SerializerOptions);
            FilesystemUtil.WriteAllTextSafe(filePath, fileText);
        }
        catch (Exception e) {
            var localLog = pluginInterface.Create<LogWrapper>();
            localLog?.Log.Error(e, $"Error trying to save file {filePath}");
        }
    }
}
