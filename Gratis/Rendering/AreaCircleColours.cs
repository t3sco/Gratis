using System;
using System.Numerics;
using Gratis.Classes;

namespace Gratis.Rendering;

/// <summary>
/// Which colours the circle around a marker is drawn in under the Gratis style. A quest objective's circle has its
/// own fill and outline settings; every other circle keeps its category colour, softened by the Area fill and Area
/// outline opacity. FATE circles are a category colour too, so they are untouched. Pure, so it is tested offline.
/// </summary>
public static class AreaCircleColours {
    /// <summary>The softest a non-quest circle is drawn, whatever the Area opacity is set to.</summary>
    public const float OtherFillCap = .10f, OtherOutlineCap = .28f;

    /// <summary>A quest circle's outline is a little heavier than the others, with a dark edge each side of it.</summary>
    public const float QuestOutlineWidth = 1.5f, HaloExtra = 1f, HaloStrength = .55f;

    /// <summary>The dark edge under a quest outline: as visible as the outline is, so an outline set to 0 shows none.</summary>
    public static Vector4 Halo(Vector4 outline) => new(0, 0, 0, HaloStrength * Math.Clamp(outline.W, 0, 1));

    public static bool IsQuest(MarkerInfo marker) => MarkerIcon.Family(marker) == MarkerIconFamily.Quest;

    public static (Vector4 Fill, Vector4 Outline) Flat(MarkerInfo marker, MapDrawingConfig config) {
        if (IsQuest(marker))
            // Area fill / outline opacity at 0 still hides a quest circle, as it did before these colours existed.
            return (config.AreaColor.W > 0 ? config.QuestAreaColor : config.QuestAreaColor with { W = 0 },
                config.AreaOutlineColor.W > 0 ? config.QuestAreaOutlineColor : config.QuestAreaOutlineColor with { W = 0 });
        var color = FlatMarkerDrawing.Color(FlatMarkerDrawing.Category(marker));
        return (color with { W = MathF.Min(config.AreaColor.W, OtherFillCap) },
            color with { W = MathF.Min(config.AreaOutlineColor.W, OtherOutlineCap) });
    }
}
