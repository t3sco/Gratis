using System.Collections.Generic;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Rendering;

namespace Gratis.Controllers;

/// <summary>
/// The Shared FATE panel's data in the running game. It only reads: the agent's tab arrays are filled when the game
/// itself asks the server for a tab (the game's Shared FATE window does that), and this never opens that window,
/// requests a tab or hooks anything. Sampled once a second into a plain snapshot, and only while the panel asks.
/// </summary>
public sealed unsafe class SharedFateGame : ISharedFateSource {
    // Bicolor Gemstone, the currency the game's window shows as "Gemstones 182/1,500".
    private const uint BicolorGemstone = 26807;
    private const int MaxTextBytes = 128;

    private SharedFateSnapshot snapshot = SharedFateSnapshot.Empty;
    private long second = long.MinValue;
    private int revision;
    private bool failureLogged;

    public string Unavailable => AgentFateProgress.Instance() == null ? SharedFateList.NotReadyReason : "";

    public SharedFateSnapshot Snapshot {
        get {
            Sample();
            return snapshot;
        }
    }

    private void Sample() {
        var now = global::System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now == second)
            return;
        second = now;
        try {
            var agent = AgentFateProgress.Instance();
            var tabs = new IReadOnlyList<SharedFateZone>[SharedFateList.TabCount];
            for (var tab = 0; tab < tabs.Length; tab++)
                tabs[tab] = agent == null ? [] : ReadTab(ref agent->Tabs[tab]);
            var gemstones = -1;
            var cap = 0;
            var currency = CurrencyManager.Instance();
            if (currency != null && currency->HasItem(BicolorGemstone)) {
                gemstones = (int)global::System.Math.Min(currency->GetItemCount(BicolorGemstone), int.MaxValue);
                cap = (int)global::System.Math.Min(currency->GetItemMaxCount(BicolorGemstone), int.MaxValue);
            }
            snapshot = new(++revision, tabs, agent == null ? 0 : agent->TabIndex, gemstones, cap);
        }
        catch (global::System.Exception error) {
            // Keep the last good snapshot rather than throwing from inside the draw.
            if (!failureLogged) {
                failureLogged = true;
                Service.Log.Warning(error, "[Gratis] Could not read the Shared FATE data.");
            }
        }
    }

    private static List<SharedFateZone> ReadTab(ref FateProgressTab tab) {
        var zones = new List<SharedFateZone>(6);
        var slots = tab.Zones;
        for (var index = 0; index < slots.Length; index++) {
            ref var zone = ref slots[index];
            // A slot the game has not filled (or that has nothing to show) has no territory.
            if (zone.TerritoryTypeId == 0)
                continue;
            zones.Add(new(zone.TerritoryTypeId, zone.DisplayOrder, Text(zone.ZoneName), zone.CurrentRank,
                zone.MaxRank, zone.FateProgress, zone.NeededFates));
        }
        return zones;
    }

    // The game's strings are UTF-8 in its own buffers: bound the length and tolerate a null or half-written buffer.
    private static string Text(in Utf8String text) {
        var length = text.StringLength;
        var pointer = text.StringPtr.Value;
        if (pointer == null || length == 0 || length > MaxTextBytes)
            return "";
        try {
            return Encoding.UTF8.GetString(pointer, (int)length).Trim('\0', ' ');
        }
        catch (global::System.Exception) {
            return "";
        }
    }
}
