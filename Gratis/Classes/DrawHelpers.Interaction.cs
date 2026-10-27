using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif
using Gratis.Rendering;

namespace Gratis.Classes;

public static partial class DrawHelpers {
    private static void ProcessInteractions(MarkerInfo marker) {
        if (MapDrawContext.Current.Config is { IsGratis: true, GratisContextMenu: true }) {
            MapDrawContext.Current.ContextMenu.Observe(marker);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                return;
        }
        if (marker is { OnRightClicked: { } rightClickAction } && ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
            rightClickAction.Invoke();
        }

        if (marker is { OnLeftClicked: { } leftClickAction } && ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
            leftClickAction.Invoke();
        }
    }

    private static unsafe void DrawTooltip(MarkerInfo marker) {
#if GRATIS_PREVIEW
        using var benchmark = BenchmarkProbe.Measure(BenchmarkSection.MarkerTooltip);
#endif
        if (MapDrawContext.Current.ReportCapture)
            return;
        using var textFont = MapDrawContext.Current.Config.IsGratis
            ? MapDrawContext.Current.GetChromeFont(14 * MapDrawContext.Current.GlobalScale, ChromeFontRole.Body)?.Push()
            : null;
        var isActivatedViaRadius = false;

        if (marker is { Radius: { } sameRadius and > 1.0f }) {
            var center = marker.Position + marker.Offset + ImGui.GetWindowPos();
            var radius = sameRadius * marker.Scale * MapDrawContext.Current.MapSizeFactor;

            if ((MapShape.FeatherWidth <= 0
                || MapShape.ContainsSymbol(center, new Vector2(radius + MapDrawContext.Current.GlobalScale))
                    && MapShape.Contains(ImGui.GetMousePos()))
                && MapDrawContext.Current.IsRadiusHovered(center, radius)) {
                isActivatedViaRadius = true;
            }
        }

        if (isActivatedViaRadius || ImGui.IsItemHovered()) {
            if (marker.PrimaryText?.Invoke() is { Length: > 0 } primaryText) {
                if (MapDrawContext.Current.Config.IsGratis && MapDrawContext.Current.Config.HoverCardStyle == 0) {
                    var category = FlatMarkerDrawing.Category(marker);
                    var label = FlatMarkerDrawing.Label(marker);
                    var secondary = marker.SecondaryText?.Invoke();
                    HoverCardDrawing.Draw(FlatMarkerDrawing.Key(marker), primaryText,
                        PlayerDrawing.IsPlayerMarker(marker) ? secondary : string.IsNullOrWhiteSpace(secondary) ? label
                            : $"{label}\n{secondary}",
                        category, marker);
                    return;
                }
                using var popupStyle = MapDrawContext.Current.Config.IsGratis ? PopupSurface.Push(PopupFaces.Chrome) : (PopupSurface.Scope?)null;
                ImGui.SetNextWindowViewport(ImGui.GetWindowViewport().ID);
                ImGui.BeginTooltip();
                if (MapDrawContext.Current.Config.IsGratis) {
                    UiMotion.DrawPopupEdge(GratisTheme.ChromeScale);
                    PopupDiagnostics.Capture("marker-tooltip", GratisTheme.Radius.Popup * GratisTheme.ChromeScale);
                }

                ImGui.Image(MapDrawContext.Current.GetIcon(marker.IconId).Handle,
                    new Vector2(32.0f) * MapDrawContext.Current.GlobalScale);

                ImGui.SameLine();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 7.5f * MapDrawContext.Current.GlobalScale);
                var cursorPosition = ImGui.GetCursorPos();
                ImGui.Text(primaryText);

                if (marker.SecondaryText?.Invoke() is { Length: > 0 } secondaryText) {
                    ImGui.SameLine();
                    ImGui.SetCursorPos(cursorPosition);
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(128 / 255.0f, 128 / 255.0f, 128 / 255.0f, 1));
                    ImGui.TextUnformatted($"\n{secondaryText}");
                    ImGui.PopStyleColor();
                }
                ImGui.EndTooltip();
            }
        }
    }
}
