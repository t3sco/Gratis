using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Classes.MapWindowComponents;
using Gratis.Rendering;
using Lumina.Excel.Sheets;

namespace Gratis.Controllers;

public sealed partial class PlaceIndexGame : ILocationMenuSource {
    private uint parentsMap = uint.MaxValue;
    private IReadOnlyList<LocationMenuParent> parents = [];

    public IPaletteSource Places => this;

    // The location card reads this every frame, so it is rebuilt only when the selected map changes.
    public unsafe IReadOnlyList<LocationMenuParent> Parents {
        get {
            var selected = AgentMap.Instance()->SelectedMapId;
            if (selected == parentsMap)
                return parents;
            parentsMap = selected;
            var map = Service.DataManager.GetExcelSheet<Map>().GetRowOrDefault(selected);
            if (map is not { } current)
                return parents = [];
            var region = current.PlaceNameRegion.ValueNullable?.Name.ExtractText() ?? "";
            var name = current.PlaceName.ValueNullable?.Name.ExtractText() ?? "";
            // AgentMap exposes a parent action, without a queryable map-ID chain.
            return parents = region.Length > 0 && region != name
                ? [new(region, MapToolbar.OpenParent)] : [];
        }
    }
}
