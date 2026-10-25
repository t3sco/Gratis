using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Gratis.Controllers;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public interface ITravelPlaces
{
    IEnumerable<PlaceEntry> Entries { get; }
    PlaceSearchContext Context { get; }
    void Refresh();
    PlaceAccess Access(PlaceEntry place);
    string Teleport(PlaceEntry place, bool confirmed);
    void Show(PlaceEntry place);
}

public sealed partial class MapAssistance(ITravelProvider provider, ITravelPlaces places,
    Func<bool> canTravel, Action save, Func<string>? blockedReason = null)
{
    public ITravelProvider Provider => available ? provider : NullTravelProvider.Instance;
    public ITravelPlaces Places => places;
    public bool InputActive { get; private set; }
    public bool OverlayOpen => hovered is not null || PinLayer?.CapturesInput == true || PinLayer?.CardVisible == true;
    public string Message { get; private set; } = "";
    public string DestinationName { get; private set; } = "";
    private PlaceEntry? hovered, pending;
    private Vector2 anchor;
    private PlaceEntry? retainedTravel;
    private Vector4 retainedTravelBounds;
    private double cardCreated, cardUntil, cardDrawn;
    private bool playerMoving;
    private Vector2? anchorWorld;
    private bool TravelReady => !playerMoving && canTravel() && !provider.IsBusy;
    public string UnavailableReason => playerMoving ? "Stop moving to teleport" : !canTravel() ? blockedReason?.Invoke() ?? "Teleport unavailable" : provider.IsBusy ? "Already travelling" : "";
    private uint pendingCost;
    private bool available;
    private readonly List<(Vector2 Min, Vector2 Max)> panelBounds = [];
    public Vector2 CardMin { get; private set; }
    public Vector2 CardMax { get; private set; }
    public Vector2 TravelButtonCenter { get; private set; }
    public Vector2 CancelButtonCenter { get; private set; }

    public void Prepare(MapDrawingConfig config, bool moving = false)
    {
        playerMoving = moving;
        InputActive = config.IsGratis && TravelReady && panelBounds.Exists(p => ImGui.IsMouseHoveringRect(p.Min, p.Max, false));
        panelBounds.Clear();
        if (!config.IsGratis) { PinLayer?.CancelEditor(); return; }
        InputActive |= PinLayer?.CapturesInput == true;
        places.Refresh(); available = provider.Available;
        if (available && !config.TravelActionsInitialized) {
            config.TravelActions = config.TravelActionsInitialized = true; save();
        }
    }

    public bool CanOffer(PlaceEntry place) => TravelReady && MapDrawContext.Current.Config.TravelActions &&
        place.Kind == PlaceKind.Aetheryte && places.Access(place).Attuned && (!place.IsShard || available);

    private PlaceEntry? FindTransport(Classes.MarkerInfo marker, string title) => places.Entries.FirstOrDefault(p =>
        p.Kind == PlaceKind.Aetheryte && p.MapId == places.Context.MapId &&
        (p.Name == title || p.AetheryteId == marker.DataId));

    public bool HoverTransport(Classes.MarkerInfo marker, string title)
    {
        if (!MapDrawContext.Current.Config.TravelActions ||
            MarkerIcon.Family(marker) is not (MarkerIconFamily.Aetheryte or MarkerIconFamily.Aethernet)) return false;
        var match = FindTransport(marker, title);
        if (match == null) return false;
        if (hovered?.Id == match.Id && TravelReady) return true;
        var reason = !places.Access(match).Attuned ? "Not yet attuned" : UnavailableReason;
        if (reason.Length == 0 && match.IsShard && !available) reason = "Lifestream unavailable";
        UiMotion.TooltipText(match.Name + (reason.Length > 0 ? " · " + reason : ""));
        return true;
    }

    public void HoverPlace(PlaceEntry place, Vector2 markerCenter)
    {
        if (!CanOffer(place)) return;
        pending = null; Message = ""; hovered = place; anchor = markerCenter; anchorWorld = null;
        cardCreated = cardDrawn = ImGui.GetTime(); cardUntil = cardCreated + 8;
        contextTarget = null;
    }

    public string Request(PlaceEntry place, bool confirmed = false)
    {
        places.Refresh();
        if (!CanOffer(place)) return Message = "Travel is unavailable now.";
        if (!provider.Available && !place.IsShard) {
            Message = places.Teleport(place, confirmed && pending?.Id == place.Id);
            pending = Message.StartsWith("Confirm ", StringComparison.Ordinal) ? place : null;
            pendingCost = places.Access(place).Cost ?? 0;
            return Message;
        }
        var access = places.Access(place);
        if (!place.IsShard && access.Cost == null) return Message = "Teleport cost is unavailable.";
        var cost = place.IsShard ? 0 : access.Cost!.Value;
        if (cost >= Math.Max(0, MapDrawContext.Current.Config.TravelConfirmGil) && cost > 0 &&
            (!confirmed || pending?.Id != place.Id || pendingCost != cost)) {
            pending = place; pendingCost = cost; return Message = $"Confirm {cost:N0} gil to {place.Name}";
        }
        pending = null;
        var destination = new TravelDestination(place.AetheryteId, place.Name, Route(place), access.Attuned, place.IsShard, cost);
        if (!provider.Travel(destination)) return Message = "Travel request was declined.";
        DestinationName = place.Name; return Message = "Travel requested.";
    }

    public static string Route(PlaceEntry place) => place.Subtitle + " > " + place.Name;

    public void Draw(IMapWindowHost host)
    {
        if (!host.Config.IsGratis) return;
        OfferClickedObjective(host);
        DrawAetheryteChip(host);
        if (host.Config.TravelActions) {
            if (available && provider.IsBusy) DrawProgress(host);
            DrawTravelCard(host);
        }
        PinLayer?.Draw(host, this);
        InputActive |= PinLayer?.InputActive == true;
        if (PinLayer?.CapturesInput == true) CapturePinKeys?.Invoke();
    }

    private PlaceEntry? pressedPlace;
    private SpotlightRequest? pressedObjective;
    private Vector2 pressPosition, pressedWorld;
    private bool markerDragged;
    private void OfferClickedObjective(IMapWindowHost host)
    {
        var context = MapDrawContext.Current;
        if (!TravelReady || !host.Config.TravelActions) {
            hovered = pending = pressedPlace = null; pressedObjective = null; return;
        }
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
            pressedPlace = null; pressedObjective = null; markerDragged = false;
            pressPosition = ImGui.GetMousePos();
            if (InputActive || context.WindowMoveInputBlocked || context.Measure.InputActive ||
                !ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)) return;
            foreach (var (marker, world) in context.MarkerTargets.Targets) {
                if (!marker.IsHovered || !context.GetIconSetting(marker.IconId).AllowClick) continue;
                if (MarkerIcon.Family(marker) is MarkerIconFamily.Aetheryte or MarkerIconFamily.Aethernet) {
                    var place = FindTransport(marker, marker.PrimaryText?.Invoke() ?? "");
                    if (place != null && CanOffer(place)) { pressedPlace = place; pressedWorld = world; break; }
                }
                if (host.Config.AetheryteChips && (marker.IsObjective || MarkerIcon.Family(marker) is MarkerIconFamily.Quest or MarkerIconFamily.Leve)) {
                    pressedObjective = new(world, host.Renderer.ViewedMapId, SpotlightSource.Quest, Icon: marker.IconId);
                    pressedWorld = world; break;
                }
            }
        }
        markerDragged |= Vector2.DistanceSquared(pressPosition, ImGui.GetMousePos()) > MathF.Pow(ImGui.GetIO().MouseDragThreshold, 2);
        if (!ImGui.IsMouseReleased(ImGuiMouseButton.Left)) return;
        if (!markerDragged) {
            if (pressedPlace is { } place) { HoverPlace(place, pressPosition); anchorWorld = pressedWorld; }
            else if (pressedObjective is { } objective) { hovered = null; OfferContextTarget(objective, host.Now); }
        }
        pressedPlace = null; pressedObjective = null;
    }

    private void DrawProgress(IMapWindowHost host)
    {
        var s = GratisTheme.ChromeScale; var (min, max) = ChromeLayout.SafeBounds();
        var size = new Vector2(MathF.Min(310 * s, max.X - min.X), 104 * s);
        var bounds = ChromeLayout.Place("travel-progress", size, host.Config.TravelCardAnchor);
        if (bounds == Vector4.Zero) return;
        Panel("##travel-progress", ChromeLayout.Position(bounds), size, () => {
            CardHeading("Travelling", DestinationName.Length == 0 ? "Your destination" : DestinationName, "");
            if (QuietButton("Cancel")) { provider.Abort(); Message = "Travel cancelled."; }
            CancelButtonCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        });
    }

    private void DismissTravel() { hovered = pending = null; panelBounds.Clear(); InputActive = PinLayer?.CapturesInput == true; }
    private void DrawTravelCard(IMapWindowHost host)
    {
        CardMin = CardMax = Vector2.Zero;
        var open = hovered != null && CanOffer(hovered);
        if (open && (host.Now > cardUntil || retainedTravelBounds != Vector4.Zero && DismissPopover(retainedTravelBounds, cardCreated))) {
            DismissTravel(); open = false;
        }
        var place = open ? hovered : retainedTravel;
        if (place == null) return;
        var s = GratisTheme.ChromeScale;
        var bounds = retainedTravelBounds;
        if (open) {
            if (anchorWorld is { } world) anchor = host.Renderer.WorldToScreen(world, ImGui.GetWindowPos());
            var (min, max) = ChromeLayout.SafeBounds();
            var requested = new Vector2(MathF.Min(332 * s, max.X - min.X),
                (Message.Length > 0 && !Message.StartsWith("Confirm ", StringComparison.Ordinal) ? 168 : 136) * s);
            if (requested.X < 230 * s) return;
            bounds = ChromeLayout.PlacePopover("hover-travel", requested, anchor);
            if (bounds == Vector4.Zero) return;
            retainedTravel = place; retainedTravelBounds = bounds;
            var over = ImGui.IsMouseHoveringRect(new(bounds.X, bounds.Y), new(bounds.Z, bounds.W), false);
            if (over || pending != null) cardUntil += Math.Max(0, host.Now - cardDrawn);
            cardDrawn = host.Now;
            CardMin = new(bounds.X, bounds.Y); CardMax = new(bounds.Z, bounds.W);
        }
        var alpha = UiMotion.Visibility(ImGui.GetID("##travel-card"), open);
        if (!open && alpha <= .001f) { retainedTravel = null; return; }
        var pos = ChromeLayout.Position(bounds); var size = new Vector2(bounds.Z - bounds.X, bounds.W - bounds.Y);
        using (var pointer = new UiMotion.VisualScope(ImGui.GetWindowDrawList(), anchor, alpha, Vector2.Zero)) DrawPointer(bounds, anchor);
        Panel("##travel-card", pos, size, () => {
            CardHeading(place.IsShard ? "Aethernet" : "Aetheryte", place.Name,
                place.IsShard ? "" : places.Access(place).Cost is { } cost ? $"{cost:N0} gil" : "Cost unavailable", place.Subtitle);
            ImGui.BeginDisabled(!open || host.Now <= cardCreated);
            if (PrimaryTravelButton(pending?.Id == place.Id ? $"Confirm {pendingCost:N0} gil" : "Teleport", GratisIcons.Compass))
                Request(place, pending?.Id == place.Id);
            ImGui.EndDisabled();
            TravelButtonCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
            ImGui.SameLine(0, GratisTheme.Gap * s);
            if (QuietButton("Dismiss")) DismissTravel();
            if (Message.Length > 0 && !Message.StartsWith("Confirm ", StringComparison.Ordinal)) ImGui.TextWrapped(Message);
            DrawTimer(pos, size, (float)(cardUntil - host.Now), host.Config.ReduceMotion);
        }, interactive: open, pivot: anchor);
    }

    private static bool DismissPopover(Vector4 bounds, double created) => ImGui.IsKeyPressed(ImGuiKey.Escape) ||
        ImGui.GetTime() > created && (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right)) &&
        !ImGui.IsMouseHoveringRect(new(bounds.X, bounds.Y), new(bounds.Z, bounds.W), false);

    private static void DrawTimer(Vector2 position, Vector2 size, float seconds, bool reduceMotion)
    {
        if (reduceMotion) return;
        var s = GratisTheme.ChromeScale; var start = position + new Vector2(12 * s, size.Y - 4 * s);
        ImGui.GetWindowDrawList().AddLine(start, start + new Vector2((size.X - 24 * s) * Math.Clamp(seconds / 8, 0, 1), 0),
            ImGui.GetColorU32(GratisTheme.WarmAccent with { W = .5f }), s);
    }

    private static void DrawPointer(Vector4 bounds, Vector2 target)
    {
        var s = GratisTheme.ChromeScale; var min = new Vector2(bounds.X, bounds.Y); var max = new Vector2(bounds.Z, bounds.W);
        var edge = Vector2.Clamp(target, min + new Vector2(12 * s), max - new Vector2(12 * s));
        Vector2 along;
        if (target.X < min.X || target.X > max.X) { edge.X = target.X < min.X ? min.X : max.X; along = new(0, 5 * s); }
        else { edge.Y = target.Y < min.Y ? min.Y : max.Y; along = new(5 * s, 0); }
        var direction = target - edge;
        if (direction.LengthSquared() < 1) return;
        var tip = edge + Vector2.Normalize(direction) * MathF.Min(9 * s, direction.Length());
        ImGui.GetWindowDrawList().AddTriangleFilled(edge - along, tip, edge + along, ImGui.GetColorU32(GratisTheme.Surface with { W = .98f }));
    }

    private static void CardHeading(string label, string name, string detail, string subtitle = "")
    {
        var context = MapDrawContext.Current; var s = GratisTheme.ChromeScale;
        using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push()) {
            var pos = ImGui.GetCursorScreenPos();
            var list = ImGui.GetWindowDrawList();
            var crystal = pos + new Vector2(5, 7) * s;
            list.AddQuad(crystal - new Vector2(0, 6) * s, crystal + new Vector2(4, 0) * s,
                crystal + new Vector2(0, 6) * s, crystal - new Vector2(4, 0) * s,
                ImGui.GetColorU32(GratisTheme.Crystal), s);
            ImGui.Dummy(new Vector2(18, 12) * s); ImGui.SameLine(0, 0);
            ImGui.TextColored(GratisTheme.Secondary, label);
            if (detail.Length > 0) {
                var x = ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X - ImGui.CalcTextSize(detail).X;
                if (x > ImGui.GetCursorPosX() + 16 * s) {
                    ImGui.SameLine(x - 6 * s);
                    var chip = ImGui.GetCursorScreenPos(); var measured = ImGui.CalcTextSize(detail);
                    list.AddRectFilled(chip - new Vector2(4, 2) * s, chip + measured + new Vector2(4, 2) * s,
                        ImGui.GetColorU32(GratisTheme.WarmAccent with { W = .10f }), GratisTheme.ButtonRadius * s);
                    ImGui.TextColored(GratisTheme.Secondary, detail);
                }
            }
        }
        using (context.GetChromeFont(GratisTheme.TravelTitleSize * s, ChromeFontRole.Title)?.Push()) {
            var displayed = ChromeControls.FitLabel(name, ImGui.GetContentRegionAvail().X);
            ImGui.TextUnformatted(displayed);
            if (displayed != name) ChromeControls.Tooltip(name);
        }
        if (subtitle.Length > 0) {
            using var secondary = context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Body)?.Push();
            ImGui.TextColored(GratisTheme.Secondary, ChromeControls.FitLabel(subtitle, ImGui.GetContentRegionAvail().X));
        }
        ImGui.Spacing();
    }

    internal static bool QuietButton(string text)
    {
        var s = GratisTheme.ChromeScale; var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(ImGui.CalcTextSize(text).X + 16 * s, GratisTheme.ControlHeight * s);
        var clicked = ChromeControls.Button(text, size);
        var list = ImGui.GetWindowDrawList();
        using var motion = UiMotion.Control(list, pos, pos + size);
        ChromeControls.Feedback(list, pos, pos + size);
        list.AddText(pos + new Vector2(8 * s, (size.Y - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(GratisTheme.Secondary), text);
        return clicked;
    }

    internal static bool ActionButton(string text, char icon)
    {
        var s = MapDrawContext.Current.GlobalScale;
        var min = ImGui.GetCursorScreenPos();
        var textSize = ImGui.CalcTextSize(text);
        var size = new Vector2(textSize.X + 38 * s, GratisTheme.ControlHeight * s);
        var clicked = ChromeControls.Button(text, size);
        var list = ImGui.GetWindowDrawList();
        using var motion = UiMotion.Control(list, min, min + size);
        list.AddRectFilled(min, min + size, ImGui.GetColorU32(new Vector4(.25f, .48f, .56f, ImGui.IsItemHovered() ? .5f : .27f)), 5 * s);
        GratisIcons.Draw(list, min + new Vector2(13 * s, size.Y / 2), icon, GratisTheme.Transport, 14 * s);
        ChromeControls.Feedback(list, min, min + size, fill: false);
        list.AddText(min + new Vector2(27 * s, (size.Y - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(GratisTheme.Primary), text);
        return clicked;
    }

    internal static bool PrimaryTravelButton(string text, char icon)
    {
        var s = GratisTheme.ChromeScale;
        var min = ImGui.GetCursorScreenPos();
        var size = new Vector2(ImGui.CalcTextSize(text).X + 40 * s, GratisTheme.ControlHeight * s);
        var clicked = ChromeControls.Button(text, size);
        var list = ImGui.GetWindowDrawList();
        using var motion = UiMotion.Control(list, min, min + size);
        var ink = GratisTheme.Surface with { W = 1 };
        list.AddRectFilled(min, min + size, ImGui.GetColorU32(GratisTheme.Crystal with { W = ImGui.IsItemHovered() ? 1 : .94f }), GratisTheme.ButtonRadius * s);
        ChromeControls.Feedback(list, min, min + size, fill: false);
        GratisIcons.Draw(list, min + new Vector2(15 * s, size.Y / 2), icon, ink, GratisTheme.IconSize * s);
        list.AddText(min + new Vector2(30 * s, (size.Y - ImGui.GetFontSize()) / 2), ImGui.GetColorU32(ink), text);
        return clicked;
    }

    internal static Vector2 ConstrainPanel(Vector2 position, Vector2 size)
    {
        var (min, max) = Classes.MapShape.Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        if (MapDrawContext.Current.Config.MapCircle) { var inset = (max - min) * .14644661f; min += inset; max -= inset; }
        var margin = new Vector2(GratisTheme.Inset * GratisTheme.ChromeScale);
        return Vector2.Clamp(position, min + margin, Vector2.Max(min + margin, max - size - margin));
    }

    internal void Panel(string id, Vector2 pos, Vector2 size, Action content, bool scroll = false, bool interactive = true, Vector2? pivot = null)
    {
        if (interactive) panelBounds.Add((pos, pos + size));
        var alpha = UiMotion.Visibility(ImGui.GetID(id), interactive);
        var s = GratisTheme.ChromeScale;
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push();
        var cursor = ImGui.GetCursorPos();
        ImGui.SetCursorScreenPos(pos);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, GratisTheme.CardRadius * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, GratisTheme.ButtonRadius * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 4) * s);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.25f, .48f, .56f, .26f));
        ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Primary);
        try {
            var flags = ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollWithMouse | (scroll ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoScrollbar) | (interactive ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoInputs);
#if GRATIS_PREVIEW
            var visible = ImGui.BeginChild(id, size, ImGuiChildFlags.AlwaysUseWindowPadding, flags);
#else
            var visible = ImGui.BeginChild(id, size, false, flags | ImGuiWindowFlags.AlwaysUseWindowPadding);
#endif
            if (visible) {
                using var motion = new UiMotion.VisualScope(ImGui.GetWindowDrawList(), pivot ?? pos, alpha,
                    UiMotion.Full ? new Vector2(0, (1 - alpha) * 6 * s) : Vector2.Zero, UiMotion.Full ? .97f + .03f * alpha : 1);
                if (scroll) UiMotion.Scroll();
                ImGui.BeginDisabled(!interactive);
                MapWindowDrawing.DrawSurface(ImGui.GetWindowDrawList(), pos, pos + size, GratisTheme.CardRadius * s, surfaceOpacity: 1);
                content();
                ImGui.EndDisabled();
                if (interactive && (ImGui.IsWindowHovered() || ImGui.IsWindowFocused() && ImGui.IsAnyItemActive())) { InputActive = true; MapDrawContext.Current.Chrome.Reveal(ImGui.GetTime()); }
            }
            ImGui.EndChild();
            ImGui.SetCursorPos(cursor); ImGui.Dummy(Vector2.Zero);
        }
        finally { ImGui.PopStyleColor(3); ImGui.PopStyleVar(4); }
    }
}
