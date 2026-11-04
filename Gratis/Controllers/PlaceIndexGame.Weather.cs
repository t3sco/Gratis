using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Rendering;
using Lumina.Excel.Sheets;

namespace Gratis.Controllers;

public sealed partial class PlaceIndexGame {
    private uint weatherTerritory;
    private long weatherWindow = -1;
    private (byte Rate, uint WeatherId)[] weatherRates = [];
    private uint currentWeather, nextWeather;
    private long nextWeatherAt;

    public unsafe string WeatherHint {
        get {
            if (!System.SystemConfig.IsGratis || !System.SystemConfig.ShowWeather
                || !Service.ClientState.IsLoggedIn || Service.ClientState.IsPvP) return "";
            var territoryId = AgentMap.Instance()->SelectedTerritoryId;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var window = WeatherForecast.WindowStart(now);
            if (weatherTerritory != territoryId || weatherWindow != window) {
                weatherTerritory = territoryId;
                weatherWindow = window;
                currentWeather = nextWeather = 0;
                var territory = Service.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId);
                if (territory is not { } t || t.IndividualWeather.RowId != 0 || t.WeatherRate.RowId == 0) return "";
                var rate = t.WeatherRate.Value;
                var rows = new List<(byte, uint)>();
                for (var i = 0; i < rate.Rate.Count; i++) rows.Add((rate.Rate[i], rate.Weather[i].RowId));
                weatherRates = rows.ToArray();
                currentWeather = WeatherForecast.Pick(weatherRates, WeatherForecast.Target(now));
                if (WeatherForecast.NextChange(weatherRates, now) is { } change) {
                    nextWeather = change.Weather;
                    nextWeatherAt = change.At;
                }
            }
            if (currentWeather == 0) return "";
            var sheet = Service.DataManager.GetExcelSheet<Weather>();
            string Name(uint id) => sheet.GetRowOrDefault(id)?.Name.ExtractText() ?? "";
            return nextWeather == 0 ? $"Now: {Name(currentWeather)}; no change forecast"
                : WeatherForecast.Line(Name(currentWeather), Name(nextWeather), nextWeatherAt - now);
        }
    }

    // RELEASE CHECK (single in-game oracle): compare this result for the current territory/time
    // against WeatherManager.GetCurrentWeather. Offline vectors cannot prove game agreement.
    public static uint WeatherComparisonHook(long unix, IReadOnlyList<(byte Rate, uint WeatherId)> rates) =>
        WeatherForecast.Pick(rates, WeatherForecast.Target(unix));
}
