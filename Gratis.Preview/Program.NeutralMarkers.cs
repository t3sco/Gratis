using System.Numerics;
using Gratis.Classes;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class Program
{
    private static void DrawNeutralMarkerScene(Vector2 frame)
    {
        var context = MapDrawContext.Current;
        var origin = ImGui.GetWindowPos();
        var list = ImGui.GetWindowDrawList();
        list.AddRectFilled(origin + new Vector2(16, 70), origin + frame - new Vector2(16, 46),
            ImGui.GetColorU32(new Vector4(.07f, .10f, .14f, .96f)), 6);
        using var font = context.GetChromeFont(14, ChromeFontRole.Body)?.Push();
        list.AddText(origin + new Vector2(32, 86), ImGui.GetColorU32(Vector4.One),
            "Game-provided NPC markers / neutral fallback beside real quests");
        var samples = new[] {
            (.23f, .35f, 60424u, "Lv. 98 Defective Turret", "Game combatant marker"),
            (.23f, .55f, 60402u, "Lv. 100 Hostile NPC", "Game boss marker"),
            (.23f, .75f, 61000u, "Unmapped event marker", "Unknown type / icon"),
            (.63f, .35f, 71021u, "Side quest available", "Actual quest"),
            (.63f, .55f, 71143u, "Feature quest in progress", "Actual quest"),
            (.63f, .75f, 60502u, "Game-marked field objective", "Actual FATE / event"),
        };
        foreach (var (x, y, id, name, caption) in samples) {
            var point = frame * new Vector2(x, y);
            var marker = new MarkerInfo {
                Position = point, Offset = Vector2.Zero, Scale = 1, IconId = id,
                IsDynamicMarker = true, MarkerType = id == 61000 ? (MarkerType)99 : id == 60502 ? MarkerType.Fate : MarkerType.Unknown,
                Category = MarkerCategory.Objective, PrimaryText = () => name,
            };
            DrawHelpers.DrawMapMarker(marker);
            list.AddText(origin + point + new Vector2(18, -7), ImGui.GetColorU32(GratisTheme.Secondary), caption);
        }
    }
}
