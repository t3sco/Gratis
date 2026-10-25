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

using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Gratis.Compat.Classes;

public interface ITabItem {
    string Name { get; }
    bool Disabled { get; }
    void Draw();
}

public class TabBar(string name, List<ITabItem> tabs, bool allowScrolling = true) {
    public void Draw() {
        using var tabBar = ImRaii.TabBar(name);
        if (!tabBar) return;

        foreach (var tab in tabs) {
            using var disabled = ImRaii.Disabled(tab.Disabled);
            DrawTabItem(tab);
        }
    }

    private void DrawTabItem(ITabItem tab) {
        using var tabItem = ImRaii.TabItem(tab.Name);
        if (!tabItem) return;

        DrawTabChild(tab);
    }

    private void DrawTabChild(ITabItem tab) {
        using var child = ImRaii.Child($"tab_{tab.Name}_child", new Vector2(0.0f, 0.0f), false, allowScrolling ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (!child) return;

        tab.Draw();
    }
}
