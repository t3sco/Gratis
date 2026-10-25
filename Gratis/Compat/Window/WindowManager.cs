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
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace Gratis.Compat.Window;

public class WindowManager : IDisposable {
    private readonly WindowSystem windowSystem;
    private readonly IDalamudPluginInterface pluginInterface;

    [PluginService] private IClientState ClientState { get; set; } = null!;

    private Window? configWindow;

    private List<Window> Windows { get; } = [];

    public WindowManager(IDalamudPluginInterface pluginInterface) {
        windowSystem = new WindowSystem(pluginInterface.Manifest.InternalName);

        this.pluginInterface = pluginInterface;
        this.pluginInterface.Inject(this);

        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
    }

    public void Dispose() {
        pluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigurationWindow;

        foreach (var window in windowSystem.Windows) {
            window.OnClose();
        }

        windowSystem.RemoveAllWindows();
    }

    public void OpenOrCreateUnique<T>(WindowFlags? flags = null) where T : Window, new() {
        if (!GetWindows<T>().Any()) {
            AddWindow(new T(), flags);
        }
        else {
            GetWindow<T>()?.UnCollapseOrToggle();
        }
    }

    public void AddWindow(Window window, WindowFlags? windowFlags = null) {
        if (Windows.Any(existingWindow => string.Equals($"{window.WindowName}##{windowSystem.Namespace}", existingWindow.WindowName, StringComparison.OrdinalIgnoreCase))) return;

        window.PluginInterface = pluginInterface;
        window.ParentWindowManager = this;
        window.WindowName += $"##{pluginInterface.InternalName}";

        if (windowFlags is { } flags) {
            window.WindowFlags = flags;
        }

        pluginInterface.Inject(window);

        window.TitleBarButtons.Add(new TitleBarButton {
            Icon = FontAwesomeIcon.Question,
            ShowTooltip = () => ImGui.SetTooltip($"Window by {pluginInterface.InternalName}{(window.AdditionalInfoTooltip is not null ? "\n\n" : "")}{window.AdditionalInfoTooltip}"),
            IconOffset = new Vector2(3.0f, 1.0f),
            Priority = -1,
        });

        if (window.WindowFlags.HasFlag(WindowFlags.OpenImmediately)) {
            var isLoggedIn = ClientState.IsLoggedIn;
            var requiresLoggedIn = window.WindowFlags.HasFlag(WindowFlags.RequireLoggedIn);

            if ( !requiresLoggedIn || (requiresLoggedIn && isLoggedIn)) {
                window.UnCollapseOrShow();
            }
        }

        if (window.WindowFlags.HasFlag(WindowFlags.IsConfigWindow)) {
            configWindow = window;
            pluginInterface.UiBuilder.OpenConfigUi += OpenConfigurationWindow;
        }

        window.Load();

        windowSystem.AddWindow(window);
        Windows.Add(window);
    }

    public void RemoveWindow(Window window) {
        windowSystem.RemoveWindow(window);
        Windows.Remove(window);
    }

    public T? GetWindow<T>() where T : Window
        => Windows.OfType<T>().FirstOrDefault();

    public IEnumerable<T> GetWindows<T>() where T : Window
        => Windows.OfType<T>();

    private void OpenConfigurationWindow() {
        if (configWindow is null) {
            pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigurationWindow;
            throw new Exception("Configuration Window Was Null, disabling ConfigUI Callback.");
        }

        if (configWindow.WindowFlags.HasFlag(WindowFlags.RequireLoggedIn) && !ClientState.IsLoggedIn) {
            return;
        }

        configWindow.UnCollapseOrShow();
    }
}
