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
    Func<bool> canTravel, Action save)
{
    public ITravelProvider Provider => available ? provider : NullTravelProvider.Instance;
    public ITravelPlaces Places => places;
    public bool InputActive { get; private set; }
    public bool OverlayOpen => hovered is not null || PinLayer?.CapturesInput == true || PinLayer?.CardVisible == true;
    public string Message { get; private set; } = "";
    public string DestinationName { get; private set; } = "";
    private PlaceEntry? hovered, pending;
    private Vector2 anchor;
    private double lastHover = double.NegativeInfinity;
    private uint pendingCost;
    private bool available;
    private readonly List<(Vector2 Min, Vector2 Max)> panelBounds = [];
    public Vector2 CardMin { get; private set; }
    public Vector2 CardMax { get; private set; }
    public Vector2 TravelButtonCenter { get; private set; }
    public Vector2 CancelButtonCenter { get; private set; }

    public void Prepare(MapDrawingConfig config)
    {
        InputActive = config.IsGratis && panelBounds.Exists(p => ImGui.IsMouseHoveringRect(p.Min, p.Max, false));
        panelBounds.Clear();
        if (!config.IsGratis) { PinLayer?.CancelEditor(); return; }
        InputActive |= PinLayer?.CapturesInput == true;
        places.Refresh(); available = provider.Available;
        if (available && !config.TravelActionsInitialized) {
            config.TravelActions = config.TravelActionsInitialized = true; save();
        }
    }

    public bool CanOffer(PlaceEntry place) => available && MapDrawContext.Current.Config.TravelActions &&
        place.Kind == PlaceKind.Aetheryte && places.Access(place).Attuned;

    public bool HoverTransport(Classes.MarkerInfo marker, string title)
    {
        if (!available || !MapDrawContext.Current.Config.TravelActions ||
            MarkerIcon.Family(marker) is not (MarkerIconFamily.Aetheryte or MarkerIconFamily.Aethernet)) return false;
        var match = places.Entries.FirstOrDefault(p => p.Kind == PlaceKind.Aetheryte &&
            p.MapId == places.Context.MapId && (p.Name == title || p.AetheryteId == marker.DataId));
        if (match == null || !CanOffer(match)) return false;
        HoverPlace(match, marker.Position + marker.Offset + ImGui.GetWindowPos());
        return true;
    }

    public void HoverPlace(PlaceEntry place, Vector2 markerCenter)
    {
        if (hovered?.Id != place.Id) { pending = null; Message = ""; }
        hovered = place; anchor = markerCenter; lastHover = ImGui.GetTime();
    }

    public string Request(PlaceEntry place, bool confirmed = false)
    {
        places.Refresh();
        if (!CanOffer(place) || !provider.Available || provider.IsBusy || !canTravel()) return Message = "Travel is unavailable now.";
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
        if (available && host.Config.TravelActions) {
            if (provider.IsBusy) DrawProgress(host);
            DrawTravelCard(host);
        }
        PinLayer?.Draw(host, this);
        InputActive |= PinLayer?.InputActive == true;
        if (PinLayer?.CapturesInput == true) CapturePinKeys?.Invoke();
    }

    private void OfferClickedObjective(IMapWindowHost host)
    {
        var context = MapDrawContext.Current;
        if (!host.Config.TravelActions || !host.Config.AetheryteChips || InputActive || context.WindowMoveInputBlocked ||
            context.Measure.InputActive || !ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem) ||
            !(ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right))) return;
        foreach (var (marker, world) in context.MarkerTargets.Targets) {
            if (!marker.IsHovered || !(marker.IsObjective || MarkerIcon.Family(marker) is MarkerIconFamily.Quest or MarkerIconFamily.Leve)) continue;
            OfferContextTarget(new(world, host.Renderer.ViewedMapId, SpotlightSource.Quest, Icon: marker.IconId), host.Now);
            break;
        }
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

    private void DrawTravelCard(IMapWindowHost host)
    {
        if (hovered == null || !CanOffer(hovered)) return;
        var s = GratisTheme.ChromeScale; var (min, max) = ChromeLayout.SafeBounds();
        var size = new Vector2(MathF.Min(310 * s, max.X - min.X), (Message.Length > 0 && !Message.StartsWith("Confirm ", StringComparison.Ordinal) ? 148 : 104) * s);
        if (size.X < 230 * s) return;
        var bounds = ChromeLayout.Place("hover-travel", size, host.Config.TravelCardAnchor);
        if (bounds == Vector4.Zero) return;
        var pos = ChromeLayout.Position(bounds);
        CardMin = pos; CardMax = pos + size;
        if (ImGui.IsMouseHoveringRect(pos - new Vector2(12 * s), pos + size + new Vector2(12 * s), false)) lastHover = ImGui.GetTime();
        if (ImGui.GetTime() - lastHover > .6) { hovered = pending = null; return; }
        Panel("##travel-card", pos, size, () => {
            CardHeading(hovered.IsShard ? "Aethernet" : "Aetheryte", hovered.Name, hovered.IsShard ? "" : places.Access(hovered).Cost is { } cost ? $"{cost:N0} gil" : "Cost unavailable");
            if (PrimaryTravelButton(pending?.Id == hovered.Id ? $"Confirm {pendingCost:N0} gil" : hovered.IsShard ? "Travel here" : "Teleport", GratisIcons.Compass))
                Request(hovered, pending?.Id == hovered.Id);
            TravelButtonCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
            ImGui.SameLine(0, GratisTheme.Gap * s);
            if (QuietButton("Dismiss")) hovered = pending = null;
            if (Message.Length > 0 && !Message.StartsWith("Confirm ", StringComparison.Ordinal)) ImGui.TextWrapped(Message);
        });
    }

    private static void CardHeading(string label, string name, string detail)
    {
        var context = MapDrawContext.Current; var s = GratisTheme.ChromeScale;
        using (context.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push()) {
            var pos = ImGui.GetCursorScreenPos();
            var list = ImGui.GetWindowDrawList();
            var crystal = pos + new Vector2(5, 7) * s;
            list.AddQuad(crystal - new Vector2(0, 6) * s, crystal + new Vector2(4, 0) * s,
                crystal + new Vector2(0, 6) * s, crystal - new Vector2(4, 0) * s,
                ImGui.GetColorU32(GratisTheme.WarmAccent), s);
            ImGui.Dummy(new Vector2(18, 12) * s); ImGui.SameLine(0, 0);
            ImGui.TextColored(GratisTheme.Secondary, label);
            if (detail.Length > 0) {
                var x = ImGui.GetWindowWidth() - ImGui.GetStyle().WindowPadding.X - ImGui.CalcTextSize(detail).X;
                if (x > ImGui.GetCursorPosX() + 8 * s) { ImGui.SameLine(x); ImGui.TextColored(GratisTheme.Secondary, detail); }
            }
        }
        using (context.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Title)?.Push()) {
            var displayed = ChromeControls.FitLabel(name, ImGui.GetContentRegionAvail().X);
            ImGui.TextUnformatted(displayed);
            if (displayed != name) ChromeControls.Tooltip(name);
        }
        ImGui.Spacing();
    }

    internal static bool QuietButton(string text)
    {
        var s = GratisTheme.ChromeScale; var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(ImGui.CalcTextSize(text).X + 16 * s, GratisTheme.ControlHeight * s);
        var clicked = ChromeControls.Button(text, size);
        var list = ImGui.GetWindowDrawList();
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

    internal void Panel(string id, Vector2 pos, Vector2 size, Action content, bool scroll = false)
    {
        panelBounds.Add((pos, pos + size));
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
            var flags = ImGuiWindowFlags.NoBackground | (scroll ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
#if GRATIS_PREVIEW
            var visible = ImGui.BeginChild(id, size, ImGuiChildFlags.AlwaysUseWindowPadding, flags);
#else
            var visible = ImGui.BeginChild(id, size, false, flags | ImGuiWindowFlags.AlwaysUseWindowPadding);
#endif
            if (visible) {
                MapWindowDrawing.DrawSurface(ImGui.GetWindowDrawList(), pos, pos + size, GratisTheme.CardRadius * s, surfaceOpacity: 1);
                content();
                if (ImGui.IsWindowHovered() || ImGui.IsAnyItemActive()) { InputActive = true; MapDrawContext.Current.Chrome.Reveal(ImGui.GetTime()); }
            }
            ImGui.EndChild();
            ImGui.SetCursorPos(cursor); ImGui.Dummy(Vector2.Zero);
        }
        finally { ImGui.PopStyleColor(3); ImGui.PopStyleVar(4); }
    }
}
