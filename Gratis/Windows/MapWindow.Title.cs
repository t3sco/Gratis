using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Map = Lumina.Excel.Sheets.Map;

namespace Gratis.Windows;

public partial class MapWindow {
    private uint lastMapId;

    private uint lastAreaPlaceNameId;

    private uint lastSubAreaPlaceNameId;

    private string compactMapTitle = string.Empty;

    private Rendering.MapLocation? cardLocation;

    private unsafe void UpdateTitle() {
        var mapChanged = lastMapId != AgentMap.Instance()->SelectedMapId;
        var areaChanged = lastAreaPlaceNameId != TerritoryInfo.Instance()->AreaPlaceNameId;
        var subAreaChanged = lastSubAreaPlaceNameId != TerritoryInfo.Instance()->SubAreaPlaceNameId;
        var locationChanged = mapChanged || areaChanged || subAreaChanged;

        if (!locationChanged)
            return;
        var subLocationString = string.Empty;
        var mapData = Service.DataManager.GetExcelSheet<Map>().GetRow(AgentMap.Instance()->SelectedMapId);
        var cardArea = string.Empty;
        if (AgentMap.Instance()->SelectedMapId == AgentMap.Instance()->CurrentMapId) {
            var place = TerritoryInfo.Instance()->SubAreaPlaceNameId != 0 ? TerritoryInfo.Instance()->SubAreaPlaceNameId
                : TerritoryInfo.Instance()->AreaPlaceNameId;
            if (place != 0)
                cardArea = Service.DataManager.GetExcelSheet<PlaceName>().GetRow(place).Name.ExtractText();
        }
        cardLocation = new(mapData.PlaceNameRegion.Value.Name.ExtractText(), mapData.PlaceName.Value.Name.ExtractText(),
            cardArea);
        var compactParts = new global::System.Collections.Generic.List<string>();
        if (System.SystemConfig.ShowRegionLabel)
            compactParts.Add(mapData.PlaceNameRegion.Value.Name.ExtractText());
        if (System.SystemConfig.ShowMapLabel)
            compactParts.Add(mapData.PlaceName.Value.Name.ExtractText());
        compactMapTitle = string.Join(" - ", compactParts);

        if (System.SystemConfig.ShowRegionLabel) {
            var mapRegionName = mapData.PlaceNameRegion.Value.Name.ExtractText();
            subLocationString += $" - {mapRegionName}";
        }

        if (System.SystemConfig.ShowMapLabel) {
            var mapName = mapData.PlaceName.Value.Name.ExtractText();
            subLocationString += $" - {mapName}";
        }

        if (AgentMap.Instance()->SelectedMapId == AgentMap.Instance()->CurrentMapId) {
            if (TerritoryInfo.Instance()->AreaPlaceNameId is not 0 && System.SystemConfig.ShowAreaLabel) {
                var areaLabel = Service.DataManager.GetExcelSheet<PlaceName>().GetRow(
                    TerritoryInfo.Instance()->AreaPlaceNameId);
                subLocationString += $" - {areaLabel.Name}";
            }

            if (TerritoryInfo.Instance()->SubAreaPlaceNameId is not 0 && System.SystemConfig.ShowSubAreaLabel) {
                var subAreaLabel = Service.DataManager.GetExcelSheet<PlaceName>().GetRow(
                    TerritoryInfo.Instance()->SubAreaPlaceNameId);
                subLocationString += $" - {subAreaLabel.Name}";
            }
        }

        WindowName = $"{subLocationString}###GratisMapWindow".TrimStart(['-', ' ']);

        lastMapId = AgentMap.Instance()->SelectedMapId;
        lastAreaPlaceNameId = TerritoryInfo.Instance()->AreaPlaceNameId;
        lastSubAreaPlaceNameId = TerritoryInfo.Instance()->SubAreaPlaceNameId;
    }

    public void RefreshTitle() {
        lastMapId = 0;
        lastAreaPlaceNameId = 0;
        lastSubAreaPlaceNameId = 0;
    }
}
