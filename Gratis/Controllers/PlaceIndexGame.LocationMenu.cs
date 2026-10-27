using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Classes.MapWindowComponents;
using Gratis.Rendering;
using Lumina.Excel.Sheets;

namespace Gratis.Controllers;

public sealed partial class PlaceIndexGame : ILocationMenuSource {
    public IPaletteSource Places => this;

    public unsafe IReadOnlyList<LocationMenuParent> Parents {
        get {
            var map = Service.DataManager.GetExcelSheet<Map>().GetRowOrDefault(AgentMap.Instance()->SelectedMapId);
            if (map is not { } current)
                return [];
            var region = current.PlaceNameRegion.ValueNullable?.Name.ExtractText() ?? "";
            var name = current.PlaceName.ValueNullable?.Name.ExtractText() ?? "";
            // AgentMap exposes a parent action, without a queryable map-ID chain.
            return region.Length > 0 && region != name
                ? [new(region, MapToolbar.OpenParent)] : [];
        }
    }
}
