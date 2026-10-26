using System.Numerics;
using Gratis.Classes;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class Program {
    private static void DrawScene(string scene, Vector2 position, Vector2 size, DrawTexture map, bool viewportOff) {
        ImGui.SetNextWindowPos(position);
        ImGui.SetNextWindowSize(size);
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground |
                                      ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings |
                                      ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0);
        ImGui.Begin("Gratis preview", flags);
        ImGui.PopStyleVar(3);
        ImGui.BeginChild("render_child", size, ImGuiChildFlags.None, flags);
        MapShape.Update(ImGui.GetWindowSize());
        var context = MapDrawContext.Current;
        context.Blur.BeginFrame(ImGui.GetFrameCount());
        context.Markers.Begin();
        if (scene == "player-heading-frame") {
            DrawHeadingFrame(size, map);
            ImGui.EndChild();
            ImGui.End();
            return;
        }
        context.Chrome.Update(context.Config, ImGui.GetTime(), false,
            scene == "chrome-hidden" ? 0 : scene == "chrome-fading" ? .5f : 1);
        if (scene == "follow-return-frame") {
            DrawFollowReturnFrame(size, map);
            MapWindowDrawing.DrawFrameEffects();
            MapToolbarDrawing.Draw(_ => { }, DrawFakeLayers);
            ImGui.EndChild();
            ImGui.End();
            return;
        }
        if (!DrawMotionMap(scene, size, map) && !DrawViewportMap(scene, size, map, viewportOff)) {
            var scale = Math.Max(size.X / map.Size.X, size.Y / map.Size.Y) * 1.15f;
            ImGui.SetCursorPos((size - map.Size * scale) / 2);
            MapTextureDrawing.DrawMapImage(map, scale);
        }
        var player = ImGui.GetWindowPos() + size * new Vector2(.45f, .63f);
        DrawMotionTrail(scene, size, player);
        if (scene is not ("markers-legend" or "markers-quests" or "markers-enemies")) {
            PlayerDrawing.DrawLookLine(player, .8f,
                context.Config is { IsGratis: true, FlatPlayer: true } ? .25f : -1.25f);
            PlayerDrawing.DrawPlayerIcon(player, MapDrawContext.Current.GetIcon(60443), .25f, 1);
        }
        if (IsMarkerScene(scene))
            DrawMarkerScene(scene, size);
        else if (scene == "markers") {
            for (var row = 0; row < 4; row++)
                for (var col = 0; col < 6; col++)
                    Marker(size * new Vector2(.09f + col * .16f, .20f + row * .20f), size,
                        (uint)(60010 + row * 6 + col), $"Marker {row * 6 + col + 1}", row == 2 ? 42 : null,
                        category: (MarkerCategory)(col + 1));
        }
        else {
            Marker(size * new Vector2(.27f, .36f), size, scene == "map-classic" ? 60401u : 60453u, "Aetheryte", null);
            Marker(size * new Vector2(.74f, .70f), size, 60071, "Survey objective", 48);
            Marker(new Vector2(5), size, 60012, "Clipping probe", null);
        }
        if (!IsMarkerScene(scene))
            Marker(size * new Vector2(.56f, .53f), size, 60071, "The Wanderer's Camp", null,
                "Level 50 - Preview marker");
        if (scene.StartsWith("map-recolor-", StringComparison.Ordinal))
            DrawRecolourMarkers(size);
        context.Markers.Flush();
        DrawMotionPings(scene, size);
        if (scene == "glass-circle") {
            var center = ImGui.GetWindowPos() + size / 2;
            MapWindowDrawing.DrawSurface(ImGui.GetWindowDrawList(), center - new Vector2(40), center + new Vector2(40),
                40);
        }
        ImGui.SetCursorPos(Vector2.Zero);
        ImGui.Dummy(Vector2.Zero);
        if (context.Config.IsGratis)
            MapWindowDrawing.DrawFrameEffects();
        MapToolbarDrawing.Draw(_ => { }, DrawFakeLayers);
        if (!context.Config.IsGratis)
            MapWindowDrawing.DrawFrameEffects();
        if (MapDrawContext.Current.Config.IsGratis)
            MapCoordinateDrawing.Draw(new Vector2(18.4f, 23.7f), true,
                scene is "hover-tooltip" or "map-glint" or "map-glint-circular" or "glass-on" or "glass-off"
                    or "glass-card" or "glass-tinted" or "glass-unavailable"
                    ? new Vector2(22.6f, 19.2f) : null);
        MapWindowDrawing.DrawTitleOnMap("Eorzea - La Noscea - Middle La Noscea###GratisMapWindow");
        FeedbackButtonDrawing.Draw();
        HoverCardDrawing.DrawTail();
        ImGui.EndChild();
        ImGui.End();
    }

    private static void Marker(Vector2 position, Vector2 mapSize, uint icon, string text, float? radius,
        string? secondary = null, MarkerCategory category = MarkerCategory.Auto) {
        DrawHelpers.DrawMapMarker(new MarkerInfo {
            Position = position, Offset = Vector2.Zero, Scale = Math.Max(mapSize.X, mapSize.Y) / 2048,
            IconId = icon,
            PrimaryText = () => text,
            SecondaryText = () => secondary,
            Radius = radius,
            Category = category,
        });
    }

    private static void DrawFakeLayers() {
        if (!ImGui.BeginPopup("Gratis_Show_Layers"))
            return;
        ImGui.MenuItem("Preview surface", "", true);
        ImGui.MenuItem("Preview underground");
        ImGui.EndPopup();
    }


}