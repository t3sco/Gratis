using System;
using System.Numerics;

namespace Gratis.Rendering;

public static class CoordinateShare {
    // Offsets are the sheet offsets (the negative of AgentMap.SelectedOffsetX/Y).
    public static Vector2 WorldToMap(Vector2 world, Vector2 offset, float factor) => new Vector2(1)
        + ((world + offset) * factor + new Vector2(1024)) * (41f / (2048 * factor));
    public static Vector2 MapToWorld(Vector2 map, Vector2 offset, float factor) =>
        ((map - Vector2.One) * (2048 * factor / 41f) - new Vector2(1024))
        / factor - offset;
    public static string Text(Vector2 point, string? mapName = null) =>
        (string.IsNullOrWhiteSpace(mapName) ? "" : mapName + " · ")
        + FormattableString.Invariant($"{point.X:F1}, {point.Y:F1}");
}