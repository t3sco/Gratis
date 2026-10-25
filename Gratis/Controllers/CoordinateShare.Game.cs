using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Rendering;
using Gratis.Compat.CommandManager;
using Lumina.Excel.Sheets;

namespace Gratis.Controllers;

internal sealed unsafe class CoordinateShareGame
{
    [PluginService] private IChatGui Chat { get; set; } = null!;
    private static CoordinateShareGame? current;
    private static Vector2 hovered;
    private static uint hoveredMap, hoveredTerritory;
    private static long hoverTick = long.MinValue;
    public static void Register()
    {
        if (current != null) return;
        current = new(); Service.PluginInterface.Inject(current);
        System.CommandManager.RegisterCommand(new CommandHandler { ActivationPath = "/link", Delegate = args => current.Link(args) });
    }
    public static void Observe(Vector2 world, uint map, uint territory)
    {
        hovered = world; hoveredMap = map; hoveredTerritory = territory; hoverTick = Environment.TickCount64;
    }
    public static string Text(uint mapId, Vector2 world)
    {
        if (Service.DataManager.GetExcelSheet<Map>().GetRowOrDefault(mapId) is not { SizeFactor: > 0 } map) return "";
        return CoordinateShare.Text(CoordinateShare.WorldToMap(world, new(map.OffsetX, map.OffsetY), map.SizeFactor / 100f),
            map.PlaceName.ValueNullable?.Name.ExtractText() ?? "Map");
    }
    public static void CopyPlayer()
    {
        var player = Service.ObjectTable.LocalPlayer;
        if (!System.SystemConfig.IsGratis || player == null) return;
        var text = Text(AgentMap.Instance()->CurrentMapId, new(player.Position.X, player.Position.Z));
        if (text.Length > 0) MapDrawContext.Current.ContextMenu.Copy(text);
    }
    private void Link(string[] args)
    {
        if (!System.SystemConfig.IsGratis) { Chat.Print("[Gratis] Map links are available in Gratis style."); return; }
        var agent = AgentMap.Instance();
        var useHover = args.Length == 0 || args[0].Equals("cursor", StringComparison.OrdinalIgnoreCase) || args[0].Equals("hovered", StringComparison.OrdinalIgnoreCase);
        useHover &= hoverTick != long.MinValue && Environment.TickCount64 - hoverTick < 250 && hoveredMap == agent->SelectedMapId;
        uint map, territory; Vector2 world;
        if (useHover) { map = hoveredMap; territory = hoveredTerritory; world = hovered; }
        else if (Service.ObjectTable.LocalPlayer is { } player) {
            map = agent->CurrentMapId; territory = agent->CurrentTerritoryId; world = new(player.Position.X, player.Position.Z);
        }
        else { Chat.Print("[Gratis] No player position is available."); return; }
        var text = Text(map, world);
        if (map == 0 || territory == 0 || text.Length == 0) { Chat.Print("[Gratis] No map position is available."); return; }
        var payload = new MapLinkPayload(territory, map, (int)MathF.Round(world.X * 1000), (int)MathF.Round(world.Y * 1000));
        var message = new SeStringBuilder().AddText("[Gratis] ").Add(payload).AddText(text).Add(RawPayload.LinkTerminator).Build();
        Chat.Print(message);
        ImGui.SetClipboardText(text);
        MapDrawContext.Current.ContextMenu.Notify("Copied " + text);
    }
}
