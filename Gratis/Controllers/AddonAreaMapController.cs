using System;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.CommandManager;
using KamiLib.Extensions;
using Gratis.Extensions;
using Gratis.Windows;

namespace Gratis.Controllers;

public unsafe class AddonAreaMapController : IDisposable {
    public AddonAreaMapController() {
        Service.Log.Debug("Beginning Listening for AddonAreaMap");
        Service.Framework.Update += AddonAreaMapListener;

        Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "AreaMap", OnAreaMapDraw);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PreShow, "AreaMap", OnAreaMapPreShow);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreHide, "AreaMap", OnAreaMapPreHide);

        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/areamap/reset",
            Delegate = _ => {
                var addon = Service.GameGui.GetAddonByName<AddonAreaMap>("AreaMap");
                if (addon is not null && addon->RootNode is not null) {
                    addon->RootNode->SetPositionFloat(addon->X, addon->Y);
                }
            },
        });
    }

    private void AddonAreaMapListener(IFramework framework) {
        var addonAreaMap = Service.GameGui.GetAddonByName<AddonAreaMap>("AreaMap");

        if (addonAreaMap is null) return;

        if (System.SystemConfig.SuppressNativeMapOpenSound) {
            addonAreaMap->ShowSoundEffectId = 0;
            addonAreaMap->DisableShowHideSoundEffects = true;
        }

        Service.Framework.Update -= AddonAreaMapListener;
    }

    public void Dispose() {
        Service.AddonLifecycle.UnregisterListener(OnAreaMapDraw);
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PreShow, "AreaMap");
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PreHide, "AreaMap");
        Service.Framework.Update -= AddonAreaMapListener;

        var addonAreaMap = Service.GameGui.GetAddonByName<AddonAreaMap>("AreaMap");
        if (addonAreaMap is not null) {
            addonAreaMap->RootNode->SetPositionFloat(addonAreaMap->X, addonAreaMap->Y);

            if (System.SystemConfig.SuppressNativeMapOpenSound) {
                addonAreaMap->ShowSoundEffectId = 23;
                addonAreaMap->DisableShowHideSoundEffects = false;
            }
        }
    }

    private void OnAreaMapPreShow(AddonEvent type, AddonArgs args) {
        if (System.ZoneSwitchController is { IsApplying: true }) return;
        System.MapWindow.WakeGhost();
        Service.Log.Verbose($"[AreaMap] AddonEventPreShow");
        System.WindowManager.GetWindow<MapWindow>()?.Open();
    }

    private void OnAreaMapPreHide(AddonEvent type, AddonArgs args) {
        if (System.ZoneSwitchController is { IsApplying: true }) return;
        System.MapWindow.WakeGhost();
        Service.Log.Verbose($"[AreaMap] AreaMapPreHide");

        if (System.SystemConfig.KeepOpen) {
            Service.Log.Verbose("[AreaMap] Keeping Open");
            return;
        }

        if (AgentMap.Instance()->AddonId is 0) {
            System.WindowManager.GetWindow<MapWindow>()?.Close();
        }
    }


    private void OnAreaMapDraw(AddonEvent type, AddonArgs args) {
        var addon = args.GetAddon<AddonAreaMap>();

        if (Service.ClientState is { IsPvP: true }) {
            if (addon->IsOffscreen())
                addon->RestorePosition();
            return;
        }

        if (addon->IsVisible || addon->RootNode->Color.A is not 0x00) {
            addon->ForceOffscreen();

            return;
        }

        if (AgentMap.Instance()->AddonId is 0) {
            addon->RestorePosition();
        }
    }
}
