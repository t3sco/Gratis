using System.Numerics;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class Program
{
    private static readonly string[] ViewportScenes = ["viewport-small", "viewport-large", "viewport-unscaled",
        "viewport-clamp-corner", "viewport-clamp-off", "viewport-circle-corner"];
    private static bool IsViewportScene(string scene) => ViewportScenes.Contains(scene);

    private static Vector2 ViewportSceneSize(string scene, Vector2 available)
    {
        var requested = scene == "viewport-small" ? new Vector2(432, 288) : new Vector2(864, 576);
        return Vector2.Min(requested, available);
    }

    private static bool DrawViewportMap(string scene, Vector2 frame, DrawTexture texture, bool viewportOff)
    {
        if (!IsViewportScene(scene)) return false;
        var config = MapDrawContext.Current.Config;
        config.ScaleWithFrame = !viewportOff && scene != "viewport-unscaled";
        config.ClampToMap = !viewportOff && scene != "viewport-clamp-off";
        config.MapCircle = scene == "viewport-circle-corner";
        Classes.MapShape.Update(frame);
        var reference = new Vector2(432, 288);
        var zoom = new ViewportZoom();
        var scale = MathF.Max(reference.X / texture.Size.X, reference.Y / texture.Size.Y) * 2.5f;
        scale = zoom.Resolve(reference, scale, config.ScaleWithFrame);
        var corner = scene is "viewport-clamp-corner" or "viewport-clamp-off" or "viewport-circle-corner";
        if (corner) scale *= .2f;
        scale = zoom.Resolve(frame, scale, config.ScaleWithFrame,
            config.ClampToMap ? ViewportMath.MinimumScale(frame, texture.Size, config.MapCircle) : .05f);
        var center = corner ? new Vector2(-300, -200) : texture.Size * new Vector2(.52f, .48f);
        if (config.ClampToMap) center = ViewportMath.ClampCenter(center, frame, texture.Size, scale, config.MapCircle);
        var origin = frame / 2 - center * scale;
        ImGui.SetCursorPos(origin);
        MapTextureDrawing.DrawMapImage(texture, scale);
        var list = ImGui.GetWindowDrawList();
        foreach (var point in new[] { new Vector2(.35f, .36f), new Vector2(.65f, .60f) }) {
            var p = ImGui.GetWindowPos() + origin + point * texture.Size * scale;
            list.AddLine(p - new Vector2(7, 0), p + new Vector2(7, 0), 0xFFFFFFFF, 1);
            list.AddLine(p - new Vector2(0, 7), p + new Vector2(0, 7), 0xFFFFFFFF, 1);
        }
        return true;
    }
}
