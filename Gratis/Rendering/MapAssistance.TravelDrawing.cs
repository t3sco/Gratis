using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class MapAssistance {
    private void DrawProgress(IMapWindowHost host) {
        var busy = provider.IsBusy;
        var alpha = UiMotion.Visibility(ImGui.GetID("##travel-progress"), busy);
        if (!busy && alpha <= .001f)
            return;
        var s = GratisTheme.ChromeScale;
        var (min, max) = ChromeLayout.SafeBounds();
        var size = new Vector2(MathF.Min(310 * s, max.X - min.X), 104 * s);
        var bounds = ChromeLayout.Place("travel-progress", size, host.Config.TravelCardAnchor);
        if (bounds == Vector4.Zero)
            return;
        Panel("##travel-progress", ChromeLayout.Position(bounds), size, () => {
            CardHeading("Travelling", DestinationName.Length == 0 ? "Your destination" : DestinationName, "");
            if (QuietButton("Cancel")) {
                provider.Abort();
                Message = "Travel cancelled.";
            }
            CancelButtonCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        }, interactive: busy);
    }

    private void DismissTravel() {
        hovered = pending = null;
        panelBounds.Clear();
        InputActive = PinLayer?.CapturesInput == true;
    }

    private void DrawTravelCard(IMapWindowHost host) {
        CardMin = CardMax = Vector2.Zero;
        var open = hovered != null && CanOffer(hovered);
        if (open && (host.Now > cardUntil
            || retainedTravelBounds != Vector4.Zero && DismissPopover(retainedTravelBounds, cardCreated))) {
            DismissTravel();
            open = false;
        }
        var place = open ? hovered : retainedTravel;
        if (place == null)
            return;
        var s = GratisTheme.ChromeScale;
        var bounds = retainedTravelBounds;
        if (open) {
            if (anchorWorld is { } world)
                anchor = host.Renderer.WorldToScreen(world, ImGui.GetWindowPos());
            var (min, max) = ChromeLayout.SafeBounds();
            var requested = new Vector2(MathF.Min(332 * s, max.X - min.X),
                (Message.Length > 0 && !Message.StartsWith("Confirm ", StringComparison.Ordinal) ? 168 : 136) * s);
            if (requested.X < 230 * s)
                return;
            bounds = ChromeLayout.PlacePopover("hover-travel", requested, anchor);
            if (bounds == Vector4.Zero)
                return;
            retainedTravel = place;
            retainedTravelBounds = bounds;
            var over = ImGui.IsMouseHoveringRect(new(bounds.X, bounds.Y), new(bounds.Z, bounds.W), false);
            if (over || pending != null)
                cardUntil += Math.Max(0, host.Now - cardDrawn);
            cardDrawn = host.Now;
            CardMin = new(bounds.X, bounds.Y);
            CardMax = new(bounds.Z, bounds.W);
        }
        var alpha = UiMotion.Visibility(ImGui.GetID("##travel-card"), open);
        if (!open && alpha <= .001f) {
            retainedTravel = null;
            return;
        }
        var pos = ChromeLayout.Position(bounds);
        var size = new Vector2(bounds.Z - bounds.X, bounds.W - bounds.Y);
        using (var pointer = new UiMotion.VisualScope(ImGui.GetWindowDrawList(), anchor, alpha, Vector2.Zero))
            DrawPointer(bounds, anchor);
        Panel("##travel-card", pos, size, () => {
            CardHeading(place.IsShard ? "Aethernet" : "Aetheryte", place.Name,
                place.IsShard ? "" : places.Access(place).Cost is { } cost ? $"{cost:N0} gil" : "Cost unavailable",
                place.Subtitle);
            ImGui.BeginDisabled(!open || host.Now <= cardCreated);
            if (PrimaryTravelButton(pending?.Id == place.Id ? $"Confirm {pendingCost:N0} gil" : "Teleport",
                GratisIcons.Compass))
                Request(place, pending?.Id == place.Id);
            ImGui.EndDisabled();
            TravelButtonCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
            ImGui.SameLine(0, GratisTheme.Gap * s);
            if (QuietButton("Dismiss"))
                DismissTravel();
            if (Message.Length > 0 && !Message.StartsWith("Confirm ", StringComparison.Ordinal))
                ImGui.TextWrapped(Message);
            DrawTimer(pos, size, (float)(cardUntil - host.Now), host.Config.ReduceMotion);
        }, interactive: open, pivot: anchor);
    }

    private static bool DismissPopover(Vector4 bounds, double created) => ImGui.IsKeyPressed(ImGuiKey.Escape)
        || ImGui.GetTime() > created
            && (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            && !ImGui.IsMouseHoveringRect(new(bounds.X, bounds.Y), new(bounds.Z, bounds.W), false);

    private static void DrawTimer(Vector2 position, Vector2 size, float seconds, bool reduceMotion) {
        if (reduceMotion)
            return;
        var s = GratisTheme.ChromeScale;
        var start = position + new Vector2(12 * s, size.Y - 4 * s);
        ImGui.GetWindowDrawList().AddLine(start,
            start + new Vector2((size.X - 24 * s) * Math.Clamp(seconds / 8, 0, 1), 0),
            ImGui.GetColorU32(GratisTheme.WarmAccent with { W = .5f }), s);
    }

    private static void DrawPointer(Vector4 bounds, Vector2 target) {
        var s = GratisTheme.ChromeScale;
        var min = new Vector2(bounds.X, bounds.Y);
        var max = new Vector2(bounds.Z, bounds.W);
        var edge = Vector2.Clamp(target, min + new Vector2(12 * s), max - new Vector2(12 * s));
        Vector2 along;
        if (target.X < min.X || target.X > max.X) {
            edge.X = target.X < min.X ? min.X : max.X;
            along = new(0, 5 * s);
        }
        else {
            edge.Y = target.Y < min.Y ? min.Y : max.Y;
            along = new(5 * s, 0);
        }
        var direction = target - edge;
        if (direction.LengthSquared() < 1)
            return;
        var tip = edge + Vector2.Normalize(direction) * MathF.Min(9 * s, direction.Length());
        ImGui.GetWindowDrawList().AddTriangleFilled(edge - along, tip, edge + along,
            ImGui.GetColorU32(GratisTheme.Surface with { W = .98f }));
    }
}