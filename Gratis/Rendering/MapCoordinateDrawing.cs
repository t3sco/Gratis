using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class MapCoordinateDrawing
{
    public static void Draw(Vector2 character, bool showCharacter, Vector2? cursor)
    {
        var context = MapDrawContext.Current;
        if (!context.Config.ShowCoordinateBar) return;
        if (context.Config.IsGratis && (context.Config.StyledCoordinates || context.Config.ShowEorzeaTime)) {
            MapWindowDrawing.DrawCoordinates(showCharacter ? character : null, cursor);
            CoordinateCopy.Draw(showCharacter ? character : null, cursor);
            return;
        }
        using var chromeAlpha = new ChromeVisibility.AlphaScope(context.Config.IsGratis, context.Chrome.ControlsAlpha(context.Config));
        if (!chromeAlpha.Visible) return;
        var size = new Vector2(DrawBinding.ContentRegionMax().X, 20 * context.GlobalScale);
        ImGui.SetCursorPos(DrawBinding.ContentRegionMax() - size);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero with { W = context.Config.CoordinateBarFade });
        var visible = ImGui.BeginChild("coordinate_child", size);
        try {
            if (!visible) return;
            var text = $"Character  {character.X:F1}  {character.Y:F1}";
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2 * context.GlobalScale);
            var textSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorPosX(DrawBinding.ContentRegionMax().X / 3 - textSize.X / 2);
            if (showCharacter) ImGui.TextColored(context.Config.CoordinateTextColor, text);
            if (cursor is { } mouse) {
                ImGui.SameLine(DrawBinding.ContentRegionMax().X * 2 / 3 - textSize.X / 2);
                ImGui.TextColored(context.Config.CoordinateTextColor, $"Cursor  {mouse.X:F1}  {mouse.Y:F1}");
            }
        }
        finally { ImGui.EndChild(); ImGui.PopStyleColor(); }
    }
}
