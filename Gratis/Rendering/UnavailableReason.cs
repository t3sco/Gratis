namespace Gratis.Rendering;

public enum MapAvailability { Ready, Loading, NoMap, NoFloor, TextureFailed, PreviewUnavailable }
public enum UnavailableAction { None, MissingCost, MissingLifestream, NotAttuned, TryOn, NoQuestDestination,
    NoUndo, ProfilesFull, PresetsFull, ToolUnavailable, EmptyFeedback, FeedbackCooldown }

// Only describe observed state: a missing texture is not proof that a zone has no map.
public static partial class UnavailableReason {
    public static string Map(MapAvailability state) => state switch {
        MapAvailability.Ready => "",
        MapAvailability.Loading => "Waiting for map data...",
        MapAvailability.NoMap => "The game has no map for this area.",
        MapAvailability.NoFloor => "The selected map or floor is not available.",
        MapAvailability.TextureFailed => "The map image could not be loaded.",
        MapAvailability.PreviewUnavailable => "No area preview available.",
        _ => "Map data is unavailable.",
    };

    public static MapAvailability MapState(bool ready, bool changingArea, bool hasMap, bool hasPath,
        bool failed) => ready ? MapAvailability.Ready : changingArea ? MapAvailability.Loading
        : !hasMap ? MapAvailability.NoMap : !hasPath ? MapAvailability.NoFloor
        : failed ? MapAvailability.TextureFailed : MapAvailability.Loading;

    public static string Action(UnavailableAction state) => state switch {
        UnavailableAction.None => "",
        UnavailableAction.MissingCost => "The teleport cost is not available yet.",
        UnavailableAction.MissingLifestream => "Lifestream is not available for aethernet travel.",
        UnavailableAction.NotAttuned => "You have not attuned to this destination.",
        UnavailableAction.TryOn => "Try on needs a loaded character outside combat and PvP.",
        UnavailableAction.NoQuestDestination => "The game has not supplied a map destination for this quest.",
        UnavailableAction.NoUndo => "No previous settings are saved to restore.",
        UnavailableAction.ProfilesFull => "All profile slots are used. Delete a profile to make room.",
        UnavailableAction.PresetsFull => "All preset slots are used. Delete a preset to make room.",
        UnavailableAction.ToolUnavailable => "This tool is not connected in the current session.",
        UnavailableAction.EmptyFeedback => "Write a message before sending.",
        UnavailableAction.FeedbackCooldown => "Wait 30 seconds between feedback messages.",
        _ => "This action is not available now.",
    };

    public static string Travel(bool attuned, bool shard, bool integration, uint? cost) => Action(
        !attuned ? UnavailableAction.NotAttuned : shard && !integration ? UnavailableAction.MissingLifestream
        : !shard && cost is null ? UnavailableAction.MissingCost : UnavailableAction.None);
}
