using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Utility;
using KamiLib.Classes;

namespace KamiLib.Configuration;

public static class Configuration {
    public static readonly JsonSerializerOptions SerializerOptions = new() {
        WriteIndented = true,
        IncludeFields = true,
    };
    
    public static T LoadCharacterFile<T>(this IDalamudPluginInterface pluginInterface, ulong contentId, string fileName) where T : new() 
        => LoadFile<T>(pluginInterface, pluginInterface.GetCharacterFileInfo(contentId, fileName).FullName);

    public static T LoadConfigFile<T>(this IDalamudPluginInterface pluginInterface, string fileName) where T : new() 
        => LoadFile<T>(pluginInterface, Path.Combine(pluginInterface.ConfigDirectory.FullName, fileName));

    public static void SaveCharacterFile<T>(this IDalamudPluginInterface pluginInterface, ulong contentId, string fileName, T file) 
        => SaveFile(pluginInterface, pluginInterface.GetCharacterFileInfo(contentId, fileName).FullName, file);

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
 
    internal static DirectoryInfo GetCharacterDirectoryInfo(this IDalamudPluginInterface pluginInterface, ulong contentId) {
        var directoryInfo = new DirectoryInfo(Path.Combine(pluginInterface.ConfigDirectory.FullName, contentId.ToString()));
        
        if (directoryInfo is { Exists: false } && contentId is not 0) {
            directoryInfo.Create();
        }
        
        directoryInfo.Refresh();

        return directoryInfo;
    }

    public static FileInfo GetCharacterFileInfo(this IDalamudPluginInterface pluginInterface, ulong contentId, string fileName) 
        => new(Path.Combine(pluginInterface.GetCharacterDirectoryInfo(contentId).FullName, fileName));

    internal static IEnumerable<CharacterConfiguration> GetAllCharacterConfigurations(this IDalamudPluginInterface pluginInterface) 
        => pluginInterface.GetAllCharacterContentIds().Select(pluginInterface.LoadCharacterConfiguration);

    private static CharacterConfiguration LoadCharacterConfiguration(this IDalamudPluginInterface pluginInterface, ulong contentId) {
        var loadedConfiguration = pluginInterface.LoadCharacterFile<CharacterConfiguration>(contentId, "System.config.json");

        if (loadedConfiguration.ContentId is 0) {
            loadedConfiguration.ContentId = contentId;
        }
        
        if (loadedConfiguration is { Version: not 2, ContentId: 0 }) {
            loadedConfiguration.Version = 2;
            loadedConfiguration.ContentId = contentId;
            pluginInterface.SaveCharacterFile(contentId, "System.config.json", loadedConfiguration);
        }

        return loadedConfiguration;
    }
    
    private static IEnumerable<ulong> GetAllCharacterContentIds(this IDalamudPluginInterface pluginInterface) {
        if (pluginInterface.ConfigDirectory is { Exists: true } directoryInfo) {
            foreach (var directory in directoryInfo.EnumerateDirectories()) {
                if (ulong.TryParse(directory.Name, out var contentId)) {
                    yield return contentId;
                }
            }
        }
    }
}
