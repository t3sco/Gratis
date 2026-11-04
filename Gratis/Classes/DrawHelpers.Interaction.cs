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

    private static string hoverTextKey = "", hoverTextTitle = "", hoverTextBody = "";
    private static double hoverTextAt;

    private static unsafe void DrawTooltip(MarkerInfo marker) {
        using var benchmark = BenchmarkProbe.Measure(BenchmarkSection.MarkerTooltip);
        if (MapDrawContext.Current.ReportCapture)
            return;
        var isActivatedViaRadius = false;

        if (marker is { Radius: { } sameRadius and > 1.0f }) {
            var center = marker.Position + marker.Offset + ImGui.GetWindowPos();
            var radius = sameRadius * marker.Scale * MapDrawContext.Current.MapSizeFactor;

            // The distance test goes first: it is the cheapest and false for almost every marker, so the feather
            // checks (all pure queries, so the order cannot change the result) only run for a mouse inside the circle.
            if (MapDrawContext.Current.IsRadiusHovered(center, radius)
                && (MapShape.FeatherWidth <= 0
                    || MapShape.ContainsSymbol(center, new Vector2(radius + MapDrawContext.Current.GlobalScale))
                        && MapShape.Contains(ImGui.GetMousePos()))) {
                isActivatedViaRadius = true;
            }
        }

        if (isActivatedViaRadius || ImGui.IsItemHovered()) {
            // Pushed only for the one hovered marker: every marker used to push and pop the font each frame.
            using var textFont = MapDrawContext.Current.Config.IsGratis
                ? MapDrawContext.Current.GetChromeFont(14 * MapDrawContext.Current.GlobalScale, ChromeFontRole.Body)?.Push()
                : null;
            if (MapDrawContext.Current.Config.IsGratis && MapDrawContext.Current.Config.HoverCardStyle == 0) {
                // The card's text comes from callbacks that look up game sheets and format strings; while the pointer
                // stays on one marker it is built about four times a second, not every frame.
                string key, title, body;
                using (BenchmarkProbe.Measure(BenchmarkSection.HoverText)) {
                    key = FlatMarkerDrawing.Key(marker);
                    var now = ImGui.GetTime();
                    // The card key alone is not enough to tell markers apart: every player dot shares one icon and no
                    // ids, so a stack of different players would all show the first one's name. Object-table markers
                    // (players, pets, enemies) are never cached; the rest also have to sit at the same place.
                    var cacheX = (int)global::System.MathF.Round(marker.Position.X);
                    var cacheY = (int)global::System.MathF.Round(marker.Position.Y);
                    var cacheKey = $"{key}@{cacheX},{cacheY}";
                    if (marker.Source != Rendering.MapMarkerSource.ObjectTable && cacheKey == hoverTextKey
                        && now - hoverTextAt < .25) {
                        title = hoverTextTitle;
                        body = hoverTextBody;
                    }
                    else {
                        title = marker.PrimaryText?.Invoke() ?? "";
                        var label = FlatMarkerDrawing.Label(marker);
                        var secondary = marker.FateProgress?.Text ?? marker.SecondaryText?.Invoke();
                        body = PlayerDrawing.IsPlayerMarker(marker) ? secondary ?? ""
                            : string.IsNullOrWhiteSpace(secondary) ? label
                            : NamesCategory(secondary, label) ? secondary : $"{label}\n{secondary}";
                        hoverTextKey = cacheKey;
                        hoverTextAt = now;
                        hoverTextTitle = title;
                        hoverTextBody = body;
                    }
                }
                if (title.Length > 0)
                    using (BenchmarkProbe.Measure(BenchmarkSection.HoverQueue))
                        HoverCardDrawing.Draw(key, title, body, FlatMarkerDrawing.Category(marker), marker);
                return;
            }
            if (marker.PrimaryText?.Invoke() is { Length: > 0 } primaryText) {
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

                if ((MapDrawContext.Current.Config.IsGratis ? marker.FateProgress?.Text ?? marker.SecondaryText?.Invoke()
                    : marker.SecondaryText?.Invoke()) is { Length: > 0 } secondaryText) {
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

    // The generic category line ("Quest / objective") is dropped when the detail text already says it.
    private static bool NamesCategory(string secondary, string label) {
        foreach (var part in label.Split(" / "))
            if (secondary.Contains(part, global::System.StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
