using System;
using System.Collections.Generic;
using System.Numerics;

namespace Gratis.Rendering;

public enum MarkerVisibilityState { Loud, Normal, Quiet, Hidden }
public enum MarkerGroup { Quests, SideQuests, Fates, Transport, Links, Services, Housing, GatheringHunts, Treasure, Players, Other, Pins }
public enum MarkerPeekKey { None, RightAlt, LeftAlt, RightCtrl, LeftCtrl }

public sealed class Lens
{
    public string Name = "My lens";
    public MarkerVisibilityState[] States = CreateStates(MarkerVisibilityState.Normal);
    public Vector4 MapTint = Vector4.Zero;
    public static MarkerVisibilityState[] CreateStates(MarkerVisibilityState state)
    {
        var values = new MarkerVisibilityState[MarkerVisibility.GroupCount];
        Array.Fill(values, state);
        return values;
    }
    public MarkerVisibilityState State(MarkerGroup group) => States is { } states && (int)group < states.Length &&
        Enum.IsDefined(states[(int)group]) ? states[(int)group] : MarkerVisibilityState.Normal;
    public Lens Copy(string name) => new() { Name = name, States = (MarkerVisibilityState[])States.Clone(), MapTint = MapTint };
}

public static class Lenses
{
    public static readonly Lens[] BuiltIn = Create();
    private static Lens[] Create()
    {
        Lens Make(string name, MarkerVisibilityState rest = MarkerVisibilityState.Normal) => new() { Name = name, States = Lens.CreateStates(rest) };
        var explore = Make("Explore");
        var quests = Make("Quests");
        quests.States[(int)MarkerGroup.Quests] = quests.States[(int)MarkerGroup.SideQuests] = MarkerVisibilityState.Loud;
        quests.States[(int)MarkerGroup.Services] = quests.States[(int)MarkerGroup.GatheringHunts] = MarkerVisibilityState.Quiet;
        var gather = Make("Gather", MarkerVisibilityState.Quiet);
        gather.States[(int)MarkerGroup.GatheringHunts] = gather.States[(int)MarkerGroup.Treasure] = MarkerVisibilityState.Loud;
        var hunt = Make("Hunt", MarkerVisibilityState.Quiet);
        hunt.States[(int)MarkerGroup.GatheringHunts] = hunt.States[(int)MarkerGroup.Fates] = MarkerVisibilityState.Loud;
        var travel = Make("Travel", MarkerVisibilityState.Quiet);
        travel.States[(int)MarkerGroup.Transport] = travel.States[(int)MarkerGroup.Links] = MarkerVisibilityState.Loud;
        var minimal = Make("Minimal", MarkerVisibilityState.Hidden);
        explore.MapTint = new Vector4(.64f, .77f, .68f, .035f);
        quests.MapTint = new Vector4(.65f, .54f, .85f, .05f);
        gather.MapTint = new Vector4(.35f, .76f, .58f, .05f);
        hunt.MapTint = new Vector4(.73f, .45f, .5f, .05f);
        travel.MapTint = new Vector4(.42f, .69f, .84f, .05f);
        return [explore, quests, gather, hunt, travel, minimal];
    }
    public static Lens Find(MapDrawingConfig config, string name)
    {
        foreach (var lens in BuiltIn) if (string.Equals(lens.Name, name, StringComparison.Ordinal)) return lens;
        if (config.CustomLenses is { } custom)
            foreach (var lens in custom) if (lens is not null && string.Equals(lens.Name, name, StringComparison.Ordinal)) return lens;
        return BuiltIn[0];
    }
    public static string Suggest(bool inDuty, bool gatheringClass, bool nearNode) => inDuty ? "Minimal" : gatheringClass && nearNode ? "Gather" : "Explore";
    public static void Cycle(MapDrawingConfig config)
    {
        var found = false;
        foreach (var lens in BuiltIn) {
            if (found) { config.ActiveLens = lens.Name; return; }
            found = lens.Name == config.ActiveLens;
        }
        if (config.CustomLenses is { } custom)
            foreach (var lens in custom) {
                if (lens is null) continue;
                if (found) { config.ActiveLens = lens.Name; return; }
                found = lens.Name == config.ActiveLens;
            }
        config.ActiveLens = "Explore";
    }
}
