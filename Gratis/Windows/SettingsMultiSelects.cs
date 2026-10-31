using System.Collections.Generic;
using System.Linq;

namespace Gratis.Windows;

/// <summary>
/// The multi-select controls, as data: each item is one raw catalog bool. Classic keeps its own rows, so the move
/// handle item names the Gratis path (ClassicMoveHandle stays a plain row for Classic).
/// </summary>
public static class SettingsMultiSelects {
    public static readonly SettingsMultiSelectDefinition[] Definitions = [
        new("centre-on", "Map", "Map links", "Centre on",
            "What moves the map when a link or target is set.", [
            new("CenterOnFlag", "Flags"),
            new("CenterOnGathering", "Gathering areas"),
            new("CenterOnQuest", "Quests"),
        ], NoneLabel: "Nothing"),

        new("hide-map", "Advanced", "Window behaviour", "Hide the map when",
            "Hide the map while any ticked situation applies.", [
            new("HideWithGameGui", "Game UI is hidden", "Game UI hidden"),
            new("HideBetweenAreas", "Between areas"),
            new("HideInCombat", "In combat"),
        ], NoneLabel: "Never"),

        new("move-window", "Advanced", "Window behaviour", "Move window with",
            "Ways to drag the map window around.", [
            new("MoveHandle", "Top-edge handle"),
            new("AltDragMovesWindow", "Alt-drag"),
            new("EnableShiftDragMove", "Shift-drag"),
        ], NoneLabel: "Nothing"),
    ];

    /// <summary>Settings a multi-select covers: Show advanced hides them, and the multi-select is their visible control.</summary>
    internal static HashSet<string> CoveredPaths() => [.. Definitions.SelectMany(d => d.Items.Select(i => i.Path))];
}
