using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Rendering;
using Lumina.Excel.Sheets;
using SheetMap = Lumina.Excel.Sheets.Map;

namespace Gratis.Windows;

public partial class MapWindow
{
    private (uint Map, uint Current, uint Area, uint Sub) bannerLocationKey;
    private SubZoneLocation? bannerLocation;
    public string SubZoneDiagnostics() => windowFrame.ZoneBanner.Diagnostics();
    unsafe SubZoneLocation? IMapWindowHost.SubZone
    {
        get {
            var agent = AgentMap.Instance(); var territory = TerritoryInfo.Instance();
            if (agent == null || territory == null) return null;
            var key = (SelectedMapId: agent->SelectedMapId, CurrentMapId: agent->CurrentMapId,
                AreaPlaceNameId: territory->AreaPlaceNameId, SubAreaPlaceNameId: territory->SubAreaPlaceNameId);
            if (key == bannerLocationKey) return bannerLocation;
            bannerLocationKey = key;
            if (key.SelectedMapId != key.CurrentMapId || key.CurrentMapId == 0) return bannerLocation = null;
            var places = Service.DataManager.GetExcelSheet<PlaceName>();
            string Name(uint id) => id == 0 ? "" : places.GetRowOrDefault(id)?.Name.ExtractText() ?? "";
            var mapName = Service.DataManager.GetExcelSheet<SheetMap>().GetRowOrDefault(key.CurrentMapId)?.PlaceName.ValueNullable?.Name.ExtractText() ?? "";
            return bannerLocation = new SubZoneLocation(key.CurrentMapId, key.AreaPlaceNameId, key.SubAreaPlaceNameId,
                mapName, Name(key.AreaPlaceNameId), Name(key.SubAreaPlaceNameId));
        }
    }
}
