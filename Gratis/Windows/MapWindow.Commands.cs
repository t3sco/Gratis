using System.Numerics;
using Gratis.Compat.CommandManager;
using Gratis.Classes;
using Gratis.Data;

namespace Gratis.Windows;

public partial class MapWindow {
    private void RegisterCommands() {
        ChromeDiagnosticCommand.Register();
        MotionDiagnosticCommand.Register();
        System.CommandManager.RegisterCommand(new ToggleCommandHandler {
            UseShowHideText = true,
            BaseActivationPath = "/map",
            EnableDelegate = _ => System.MapWindow.UnCollapseOrShow(),
            DisableDelegate = _ => System.MapWindow.Close(),
            ToggleDelegate = _ => System.MapWindow.UnCollapseOrToggle(),
        });

        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/map/follow",
            Delegate = _ => {
                System.SystemConfig.FollowPlayer = true;
                SystemConfig.Save();
            },
        });

        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/map/unfollow",
            Delegate = _ => {
                System.SystemConfig.FollowPlayer = false;
                SystemConfig.Save();
            },
        });

        System.CommandManager.RegisterCommand(new ToggleCommandHandler {
            BaseActivationPath = "/autofollow",
            EnableDelegate = _ => {
                System.SystemConfig.FollowOnOpen = true;
                SystemConfig.Save();
            },
            DisableDelegate = _ => {
                System.SystemConfig.FollowOnOpen = false;
                SystemConfig.Save();
            },
            ToggleDelegate = _ => {
                System.SystemConfig.FollowOnOpen = !System.SystemConfig.FollowOnOpen;
                SystemConfig.Save();
            },
        });

        System.CommandManager.RegisterCommand(new ToggleCommandHandler {
            BaseActivationPath = "/keepopen",
            EnableDelegate = _ => {
                System.SystemConfig.KeepOpen = true;
                SystemConfig.Save();
            },
            DisableDelegate = _ => {
                System.SystemConfig.KeepOpen = false;
                SystemConfig.Save();
            },
            ToggleDelegate = _ => {
                System.SystemConfig.KeepOpen = !System.SystemConfig.KeepOpen;
                SystemConfig.Save();
            },
        });

        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/center/player",
            Delegate = _ => {
                if (Service.ObjectTable.LocalPlayer is { } localPlayer) {
                    System.MapRenderer.CenterOnGameObject(localPlayer);
                }
            },
        });

        System.CommandManager.RegisterCommand(new CommandHandler {
            ActivationPath = "/center/map",
            Delegate = _ => {
                System.SystemConfig.FollowPlayer = false;
                System.MapRenderer.DrawOffset = Vector2.Zero;
            },
        });
    }
}