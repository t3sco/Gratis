using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Interface.Textures;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiLib.Configuration;
using NetStone;

namespace KamiLib.Extensions;

public static class NetStoneExtensions {
    internal static async Task<ISharedImmediateTexture?> TryGetProfilePicture(HttpClient httpClient, LodestoneClient lodestoneClient, IDalamudPluginInterface pluginInterface, ITextureProvider textureProvider, IPluginLog log, CharacterConfiguration characterConfiguration) {
        try {
            if (characterConfiguration.ContentId is 0) return null;

            var profilePictureFileInfo = pluginInterface.GetProfilePictureFileInfo(characterConfiguration);
        
            if (profilePictureFileInfo is { Exists: true } && !characterConfiguration.PurgeProfilePicture) {
                return textureProvider.GetFromFile(profilePictureFileInfo.FullName);
            }

            if (await lodestoneClient.TryGetLodestoneCharacter(pluginInterface, log, characterConfiguration) is { } lodestoneCharacter) {
                if (await httpClient.GetAsync(lodestoneCharacter.Portrait) is { } lodestoneRequestResponse) {
                    await using (var mediaStream = await lodestoneRequestResponse.Content.ReadAsStreamAsync()) {
                        await using (var fileStream = new FileStream(profilePictureFileInfo.FullName, FileMode.Create)) {
                            await mediaStream.CopyToAsync(fileStream);
                        }
                    }
                    
                    profilePictureFileInfo.Refresh();
                    
                    return profilePictureFileInfo is { Exists: false } ? null : textureProvider.GetFromFile(profilePictureFileInfo.FullName);
                }
            }
            else {
                return null; 
            }

            return null;
        }
        catch (Exception e) {
            log.Error(e, $"Exception trying to load profile picture for {characterConfiguration.CharacterName}@{characterConfiguration.CharacterWorld}");
            return null;
        }
    }
}