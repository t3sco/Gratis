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

using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace Gratis.Compat.Classes;

public unsafe class Teleporter {
	[PluginService] private IDataManager DataManager { get; set; } = null!;
	[PluginService] private IChatGui ChatGui { get; set; } = null!;

	private readonly string pluginName;

	public Teleporter(IDalamudPluginInterface pluginInterface) {
		pluginInterface.Inject(this);
		pluginName = pluginInterface.InternalName;
	}

	public void Teleport(uint aetheryteId) {
		var aetheryte = DataManager.GetExcelSheet<Aetheryte>().GetRow(aetheryteId);
		Telepo.Instance()->Teleport(aetheryteId, 0);
		ChatGui.Print(new XivChatEntry {
			Message = new SeStringBuilder()
				.AddUiForeground($"[{pluginName}] ", 45)
				.AddUiForeground("[Teleport] ", 62)
				.AddText("Teleporting to ")
				.AddUiForeground(aetheryte.PlaceName.Value.Name.ToString(), 576)
				.Build(),
		});
	}
}
