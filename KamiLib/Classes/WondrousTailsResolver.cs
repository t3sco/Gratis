using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace KamiLib.Classes;

public static class WondrousTailsResolver {
    private static IEnumerable<InstanceContent> GetInstancesForOrderData(this IDataManager dataManager, uint orderId) {
        var orderData = dataManager.GetExcelSheet<WeeklyBingoOrderData>().GetRow(orderId);

        switch (orderData.Type) {
            case 0:
                return [orderData.Data.GetValueOrDefault<InstanceContent>() ?? throw new Exception("Tried to parse non-instance content data.")];

            case 1:
                return dataManager.GetExcelSheet<ContentFinderCondition>()
                    .Where(cfc => cfc.ContentType.RowId is 2)
                    .Where(cfc => cfc.ClassJobLevelRequired == orderData.Data.RowId)
                    .AsInstanceContent();

            case 2:
                return dataManager.GetExcelSheet<ContentFinderCondition>()
                    .Where(cfc => cfc.ContentType.RowId is 2)
                    .Where(cfc => cfc.ClassJobLevelRequired >= orderData.Unknown2)
                    .Where(cfc => cfc.ClassJobLevelRequired <= orderData.Data.RowId)
                    .AsInstanceContent();

            case 3:
                return dataManager.GetExcelSheet<InstanceContent>()
                    .Where(instance => instance.InstanceContentType.RowId == orderData.Data.RowId);

            case 4:
                return dataManager.GetExcelSheet<WeeklyBingoMultipleOrder>().GetRow(orderData.Data.RowId)
                    .Content
                    .Where(content => content is { IsValid: true, RowId: not 0 })
                    .Select(content => content.Value);

            case 5:
                return dataManager.GetExcelSheet<ContentFinderCondition>()
                    .Where(cfc => cfc.ContentType.RowId is 2)
                    .Where(cfc => cfc.ClassJobLevelRequired >= orderData.Unknown2)
                    .Where(cfc => cfc.ClassJobLevelRequired <= orderData.Data.RowId)
                    .Where(cfc => !(cfc.ClassJobLevelRequired >= 50 && cfc.ClassJobLevelRequired % 10 is 0))
                    .AsInstanceContent();

            case 6:
                return dataManager.GetExcelSheet<ContentFinderCondition>()
                    .Where(cfc => cfc is { ContentType.RowId: 2, HighLevelRoulette: true })
                    .Where(cfc => cfc.ClassJobLevelRequired >= orderData.Unknown2)
                    .Where(cfc => cfc.ClassJobLevelRequired <= orderData.Data.RowId)
                    .Where(cfc => cfc.ClassJobLevelRequired % 10 is 0)
                    .AsInstanceContent();

            case 7:
                return dataManager.GetExcelSheet<ContentFinderCondition>()
                    .Where(cfc => cfc.ContentType.RowId is 4)
                    .Where(cfc => cfc.ClassJobLevelRequired >= orderData.Unknown2)
                    .Where(cfc => cfc.ClassJobLevelRequired <= orderData.Data.RowId)
                    .AsInstanceContent();

            case 8:
                return dataManager.GetExcelSheet<ContentFinderCondition>()
                    .Where(cfc => cfc is { ContentType.RowId: 5, ContentMemberType.RowId: 4 })
                    .Where(cfc => cfc.ClassJobLevelRequired >= orderData.Unknown2)
                    .Where(cfc => cfc.ClassJobLevelRequired <= orderData.Data.RowId)
                    .AsInstanceContent();

            case 9:
                return dataManager.GetExcelSheet<ContentFinderCondition>()
                    .Where(cfc => cfc is { ContentType.RowId: 5, ContentMemberType.RowId: 3, NormalRaidRoulette: true })
                    .Where(cfc => cfc.ClassJobLevelRequired >= orderData.Unknown2)
                    .Where(cfc => cfc.ClassJobLevelRequired <= orderData.Data.RowId)
                    .AsInstanceContent();
        }

        return [];
    }

    public static IEnumerable<ContentFinderCondition> GetDutiesForOrderData(this IDataManager dataManager, uint orderId) {
        foreach (var instanceContent in GetInstancesForOrderData(dataManager, orderId)) {
            var duty = dataManager.GetExcelSheet<ContentFinderCondition>().Where(cfc => MatchInstanceContent(cfc, instanceContent));
            foreach (var dutyContent in duty) {
                yield return dutyContent;
            }
        }
    }

    public static IEnumerable<TerritoryType> GetTerritoriesForOrderData(this IDataManager dataManager, uint orderId) {
        foreach (var duty in GetDutiesForOrderData(dataManager, orderId)) {
            if (duty.TerritoryType.IsValid) {
                yield return duty.TerritoryType.Value;
            }
        }
    }

    private static bool MatchInstanceContent(ContentFinderCondition cfc, InstanceContent instanceContent) {
        if (cfc.Content.GetValueOrDefault<InstanceContent>() is not { } cfcInstanceContent) return false;

        return cfcInstanceContent.RowId == instanceContent.RowId;
    }
    
    private static IEnumerable<InstanceContent> AsInstanceContent(this IEnumerable<ContentFinderCondition> enumerable)
        => enumerable.Select(cfc => cfc.Content.GetValueOrDefault<InstanceContent>())
            .Where(instanceContent => instanceContent.HasValue)
            .OfType<InstanceContent>();
}
