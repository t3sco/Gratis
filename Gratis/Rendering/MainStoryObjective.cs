using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Gratis.Controllers;

namespace Gratis.Rendering;

public sealed class MainStoryObjective {
    public static readonly PlaceEntry Action = new("action:main-story", PlaceKind.Quest, "Show main story objective",
        "Spotlight the main scenario objective on this map", 0, 0, Aliases: "msq main scenario story objective");
    public static bool IsObjective(PlaceEntry place) => place.Kind == PlaceKind.Quest && place.MapId != 0
        && place.World is { } world && float.IsFinite(world.X) && float.IsFinite(world.Y)
        && MarkerIcon.TryQuest(place.Icon, out var quest) && quest.MainScenario
        && quest.State is QuestMarkerState.InProgress or QuestMarkerState.Complete;
    public static PlaceEntry
        ? Select(IEnumerable<PlaceEntry> entries, uint mapId, Vector2? player) =>
            entries.Where(p => p.MapId == mapId && IsObjective(p)).OrderBy(p => player is { } position
                ? Vector2.DistanceSquared(position, p.World!.Value) : 0).ThenBy(p => p.Id,
                StringComparer.Ordinal).FirstOrDefault();
    public string Message { get; private set; } = "Not requested";
    public PlaceEntry? Target { get; private set; }
    public int OnMap { get; private set; }
    public void Show(MapRendererFrame renderer, IEnumerable<PlaceEntry> entries, PlaceSearchContext search) {
        var context = MapDrawContext.Current;
        if (!context.Config.IsGratis) {
            Message = "Main story Spotlight is available in Gratis style.";
            return;
        }
        var objectives = entries.Concat(context.MarkerTargets.Entries).Where(IsObjective).ToArray();
        OnMap = objectives.Count(p => p.MapId == renderer.ViewedMapId);
        Target = Select(objectives, renderer.ViewedMapId, search.Player);
        context.Assistance?.ClearStoryTarget();
        if (Target is { World: { } world } target
            && renderer.BeginSpotlight(new(world, target.MapId, SpotlightSource.Quest, Icon: target.Icon)))
            Message = "Main story objective: " + target.Name;
        else {
            Target = objectives.Where(p => p.MapId != renderer.ViewedMapId).OrderBy(p => p.Id,
                StringComparer.Ordinal).FirstOrDefault();
            Message = "No main story objective on this map.";
            if (Target != null) {
                Message += " Tracked objective is on another map.";
                context.Assistance?.OfferStoryTarget(Target, renderer.ViewedMapId, ImGuiTime());
            }
        }
        context.ContextMenu.Notify(Message);
    }

    private static double ImGuiTime() =>
#if GRATIS_PREVIEW
        ImGuiNET.ImGui.GetTime();
#else
        Dalamud.Bindings.ImGui.ImGui.GetTime();
#endif
    public string Diagnostics() => $"msq on-map={OnMap} target={Target?.Id ?? "none"} "
        + $"map={Target?.MapId.ToString() ?? "none"} result={Message}";
}