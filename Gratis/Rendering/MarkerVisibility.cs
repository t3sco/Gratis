using System;
using System.Numerics;
using Gratis.Classes;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed class MarkerVisibility
{
    public const int GroupCount = 11;
    public static readonly string[] Names = ["Quests", "Side quests / leves", "FATEs", "Aetherytes / transport", "Exits / links", "Shops / services", "Housing", "Gathering / hunts", "Treasure", "Party / players", "Other"];
    public static readonly char[] Icons = [GratisIcons.Flag, GratisIcons.BookOpen, GratisIcons.Swords, GratisIcons.Compass,
        GratisIcons.DoorOpen, GratisIcons.Store, GratisIcons.House, GratisIcons.Pickaxe, GratisIcons.Package, GratisIcons.LocateFixed, GratisIcons.MapPin];
    private readonly MarkerVisibilityState[] states = new MarkerVisibilityState[GroupCount];
    private readonly Vector2[] values = new Vector2[GroupCount * 2];
    private readonly Vector2[] starts = new Vector2[GroupCount * 2];
    private readonly Vector2[] targets = new Vector2[GroupCount * 2];
    private readonly double[] changed = new double[GroupCount * 2];
    public int[] Counts { get; } = new int[GroupCount];
    public MarkerVisibilityState?[] Overrides { get; } = new MarkerVisibilityState?[GroupCount];
    public bool Peek { get; private set; }
    public string EffectiveLens { get; private set; } = "Explore";
    private bool initialized;
    private string? lastLens;
    private int lastFrame = -1;

    public static MarkerGroup Group(MarkerInfo marker) => MarkerIcon.Family(marker) switch {
        MarkerIconFamily.Quest => MarkerIcon.TryQuest(marker.IconId, out var quest) && !quest.MainScenario && !quest.Feature ? MarkerGroup.SideQuests : MarkerGroup.Quests,
        MarkerIconFamily.Leve or MarkerIconFamily.Lore => MarkerGroup.SideQuests,
        MarkerIconFamily.Fate or MarkerIconFamily.FateBoss or MarkerIconFamily.FateCollect or MarkerIconFamily.FateDefend or MarkerIconFamily.FateEscort or MarkerIconFamily.FateSpecial => MarkerGroup.Fates,
        MarkerIconFamily.Aetheryte or MarkerIconFamily.Aethernet or MarkerIconFamily.Anchor or MarkerIconFamily.Chocobo => MarkerGroup.Transport,
        MarkerIconFamily.Exit or MarkerIconFamily.Duty => MarkerGroup.Links,
        MarkerIconFamily.Shop or MarkerIconFamily.Mender or MarkerIconFamily.Inn or MarkerIconFamily.Mail or MarkerIconFamily.Bell or MarkerIconFamily.Market or MarkerIconFamily.Cards or MarkerIconFamily.Gate or MarkerIconFamily.Novice or MarkerIconFamily.Guildhest => MarkerGroup.Services,
        MarkerIconFamily.Housing or MarkerIconFamily.Stable => MarkerGroup.Housing,
        MarkerIconFamily.Gathering or MarkerIconFamily.Botany or MarkerIconFamily.Fishing or MarkerIconFamily.Hunt => MarkerGroup.GatheringHunts,
        MarkerIconFamily.Treasure => MarkerGroup.Treasure,
        MarkerIconFamily.Party or MarkerIconFamily.Alliance or MarkerIconFamily.Player or MarkerIconFamily.Pet => MarkerGroup.Players,
        _ => MarkerGroup.Other,
    };
    public static bool IsPriority(MarkerInfo marker) => marker.IsTarget || marker.IsFlag || marker.IsTracked || marker.IsHovered || marker.IsSpotlighted || marker.IsObjective ||
        marker.MarkerType is MarkerType.Fate or MarkerType.Stellar || MarkerIcon.Family(marker) is MarkerIconFamily.Quest or MarkerIconFamily.Leve or
        MarkerIconFamily.Fate or MarkerIconFamily.FateBoss or MarkerIconFamily.FateCollect or MarkerIconFamily.FateDefend or MarkerIconFamily.FateEscort or
        MarkerIconFamily.FateSpecial or MarkerIconFamily.Aetheryte or MarkerIconFamily.Aethernet or MarkerIconFamily.Flag or MarkerIconFamily.Party or MarkerIconFamily.Alliance;

    public static MarkerVisibilityState Resolve(MarkerVisibilityState lens, MarkerVisibilityState? filter, bool priority, bool peek) =>
        peek ? MarkerVisibilityState.Normal : filter == MarkerVisibilityState.Hidden ? MarkerVisibilityState.Hidden :
        priority && (filter ?? lens) == MarkerVisibilityState.Hidden ? MarkerVisibilityState.Normal : filter ?? lens;
    public static Vector2 Presentation(MarkerVisibilityState state) => state switch {
        MarkerVisibilityState.Loud => new Vector2(1, 1.1f), MarkerVisibilityState.Quiet => new Vector2(.45f, .72f),
        MarkerVisibilityState.Hidden => new Vector2(0, .65f), _ => Vector2.One,
    };
    public void BeginFrame(MapDrawContext context)
    {
        if (lastFrame == ImGui.GetFrameCount()) return;
        lastFrame = ImGui.GetFrameCount();
        Array.Clear(Counts);
        var config = context.Config;
        var peek = config.IsGratis && !ImGui.GetIO().WantTextInput && (ImGui.IsWindowHovered() || ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows)) && IsPeekDown(config.PeekKey);
        if (config.IsGratis && config.LensCycleKey != 0 && !ImGui.GetIO().WantTextInput && ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows) &&
            Enum.IsDefined((ImGuiKey)config.LensCycleKey) && ImGui.IsKeyPressed((ImGuiKey)config.LensCycleKey, false)) { Lenses.Cycle(config); context.SaveConfig(); }
        var selected = config.LensAuto ? Lenses.Suggest(context.LensInDuty, context.LensGatheringClass, context.LensNearNode) : config.ActiveLens;
        Advance(config, ImGui.GetTime(), peek, selected);
    }
    internal void Advance(MapDrawingConfig config, double now, bool peek, string? selected = null)
    {
        EffectiveLens = selected ?? config.ActiveLens;
        if (lastLens != EffectiveLens) { Array.Clear(Overrides); lastLens = EffectiveLens; }
        Peek = peek;
        var lens = Lenses.Find(config, EffectiveLens);
        for (var i = 0; i < GroupCount; i++) {
            states[i] = lens.State((MarkerGroup)i);
            for (var p = 0; p < 2; p++) {
                var index = i * 2 + p;
                var target = Presentation(Resolve(states[i], Overrides[i], p != 0, peek));
                values[index] = Vector2.Lerp(starts[index], targets[index], (float)Math.Clamp((now - changed[index]) / .15, 0, 1));
                if (!initialized || config.ReduceMotion) values[index] = starts[index] = targets[index] = target;
                else if (targets[index] != target) { starts[index] = values[index]; targets[index] = target; changed[index] = now; }
            }
        }
        initialized = true;
    }
    public MarkerVisibilityState State(MarkerGroup group, bool priority = false) => Resolve(initialized ? states[(int)group] : MarkerVisibilityState.Normal, Overrides[(int)group], priority, Peek);
    public Vector2 Visual(MarkerInfo marker)
    {
        if (!MapDrawContext.Current.Config.IsGratis) return Vector2.One;
        var group = Group(marker);
        var priority = IsPriority(marker);
        if (priority && State(group, true) != MarkerVisibilityState.Hidden && values[(int)group * 2 + 1].X == 0) return Vector2.One;
        var visual = values[(int)group * 2 + (priority ? 1 : 0)];
        if (EffectiveLens == "Explore" && MarkerIcon.Family(marker) == MarkerIconFamily.Landmark && Overrides[(int)group] is null && !Peek) visual.Y *= 1.1f;
        return initialized ? visual : Vector2.One;
    }
    public void Count(MarkerInfo marker) => Counts[(int)Group(marker)]++;
    public void Toggle(MarkerGroup group) => Overrides[(int)group] = State(group) == MarkerVisibilityState.Hidden ? MarkerVisibilityState.Normal : MarkerVisibilityState.Hidden;
    public void Emphasize(MarkerGroup group) => Overrides[(int)group] = State(group) == MarkerVisibilityState.Loud ? MarkerVisibilityState.Quiet : MarkerVisibilityState.Loud;
    public void Solo(MarkerGroup group) { for (var i = 0; i < GroupCount; i++) Overrides[i] = i == (int)group ? MarkerVisibilityState.Normal : MarkerVisibilityState.Quiet; }
    public void ResetFilters() => Array.Clear(Overrides);
    public Lens Snapshot(string name) { var lens = new Lens { Name = name }; for (var i = 0; i < GroupCount; i++) lens.States[i] = State((MarkerGroup)i); return lens; }
    private static bool IsPeekDown(MarkerPeekKey key) => key != MarkerPeekKey.None && ImGui.IsKeyDown(key switch {
        MarkerPeekKey.LeftAlt => ImGuiKey.LeftAlt, MarkerPeekKey.RightCtrl => ImGuiKey.RightCtrl,
        MarkerPeekKey.LeftCtrl => ImGuiKey.LeftCtrl, _ => ImGuiKey.RightAlt,
    });
}
