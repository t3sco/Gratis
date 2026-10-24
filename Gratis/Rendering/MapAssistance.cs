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
        DrawAetheryteChip(host);
        if (available && host.Config.TravelActions) {
            if (provider.IsBusy) DrawProgress(host);
            DrawTravelCard(host);
        }
        PinLayer?.Draw(host, this);
        InputActive |= PinLayer?.InputActive == true;
        if (PinLayer?.CapturesInput == true) CapturePinKeys?.Invoke();
    }

    private void DrawProgress(IMapWindowHost host)
    {
        var s = MapDrawContext.Current.GlobalScale;
        var size = new Vector2(MathF.Min(310 * s, ImGui.GetWindowWidth() * .66f), 40 * s);
        var position = ImGui.GetWindowPos() + new Vector2((ImGui.GetWindowWidth() - size.X) / 2, 94 * s);
        Panel("##travel-progress", position, size, () => {
            GratisIcons.Draw(ImGui.GetWindowDrawList(), ImGui.GetCursorScreenPos() + new Vector2(8, 9) * s,
                GratisIcons.Repeat, GratisTheme.Transport, 15 * s);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 24 * s);
            ImGui.TextUnformatted("Travelling to " + (DestinationName.Length == 0 ? "destination..." : DestinationName));
            ImGui.SameLine();
            if (ImGui.SmallButton("Cancel")) { provider.Abort(); Message = "Travel cancelled."; }
            CancelButtonCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        });
    }

    private void DrawTravelCard(IMapWindowHost host)
    {
        if (hovered == null || !CanOffer(hovered)) return;
        var s = MapDrawContext.Current.GlobalScale;
        var size = new Vector2(MathF.Min(348 * s, ImGui.GetWindowWidth() - 24 * s), (Message.Length > 0 ? 124 : 104) * s);
        if (size.X < 170 * s) return;
        var pos = ConstrainPanel(anchor + new Vector2(22, 12) * s, size);
        CardMin = pos; CardMax = pos + size;
        if (ImGui.IsMouseHoveringRect(pos - new Vector2(12 * s), pos + size + new Vector2(12 * s), false)) lastHover = ImGui.GetTime();
        if (ImGui.GetTime() - lastHover > .6) { hovered = pending = null; return; }
        Panel("##travel-card", pos, size, () => {
            using (MapDrawContext.Current.GetChromeFont(15 * s, ChromeFontRole.Title)?.Push()) ImGui.TextUnformatted(hovered.Name);
            using (MapDrawContext.Current.GetChromeFont(11 * s, ChromeFontRole.Body)?.Push()) {
                ImGui.PushTextWrapPos(ImGui.GetWindowWidth() - 12 * s);
                ImGui.TextColored(GratisTheme.Secondary, Route(hovered)); ImGui.PopTextWrapPos();
            }
            ImGui.Spacing();
            if (ActionButton(pending?.Id == hovered.Id ? $"Confirm {pendingCost:N0} gil" : "Travel here", GratisIcons.Compass))
                Request(hovered, pending?.Id == hovered.Id);
            TravelButtonCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
            if (places.Access(hovered).Cost is { } cost && !hovered.IsShard) {
                ImGui.SameLine(); ImGui.TextDisabled($"{cost:N0} gil · attuned");
            }
            else { ImGui.SameLine(); ImGui.TextDisabled("Aethernet · attuned"); }
            if (Message.Length > 0 && !Message.StartsWith("Confirm ", StringComparison.Ordinal)) ImGui.TextDisabled(Message);
        });
    }

    internal static bool ActionButton(string text, char icon)
    {
        var s = MapDrawContext.Current.GlobalScale;
        var min = ImGui.GetCursorScreenPos();
        var textSize = ImGui.CalcTextSize(text);
        var size = textSize + new Vector2(38, 12) * s;
        var clicked = ImGui.InvisibleButton(text, size);
        var list = ImGui.GetWindowDrawList();
        list.AddRectFilled(min, min + size, ImGui.GetColorU32(new Vector4(.25f, .48f, .56f, ImGui.IsItemHovered() ? .5f : .27f)), 5 * s);
        GratisIcons.Draw(list, min + new Vector2(13 * s, size.Y / 2), icon, GratisTheme.Transport, 14 * s);
        list.AddText(min + new Vector2(27, 6) * s, ImGui.GetColorU32(GratisTheme.Primary), text);
        return clicked;
    }

    internal static Vector2 ConstrainPanel(Vector2 position, Vector2 size)
    {
        var (min, max) = Classes.MapShape.Bounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        if (MapDrawContext.Current.Config.MapCircle) { var inset = (max - min) * .14644661f; min += inset; max -= inset; }
        var margin = new Vector2(8 * MapDrawContext.Current.GlobalScale);
        return Vector2.Clamp(position, min + margin, Vector2.Max(min + margin, max - size - margin));
    }

    internal void Panel(string id, Vector2 pos, Vector2 size, Action content)
    {
        panelBounds.Add((pos, pos + size));
        var s = MapDrawContext.Current.GlobalScale;
        using var font = MapDrawContext.Current.GetChromeFont(12 * s, ChromeFontRole.Body)?.Push();
        var cursor = ImGui.GetCursorPos();
        ImGui.SetCursorScreenPos(pos);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 10) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10 * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5 * s);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.25f, .48f, .56f, .26f));
        ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Primary);
        try {
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
#if GRATIS_PREVIEW
            var visible = ImGui.BeginChild(id, size, ImGuiChildFlags.AlwaysUseWindowPadding, flags);
#else
            var visible = ImGui.BeginChild(id, size, false, flags | ImGuiWindowFlags.AlwaysUseWindowPadding);
#endif
            if (visible) {
                MapWindowDrawing.DrawSurface(ImGui.GetWindowDrawList(), pos, pos + size, 10 * s, surfaceOpacity: 1);
                content();
                if (ImGui.IsWindowHovered() || ImGui.IsAnyItemActive()) { InputActive = true; MapDrawContext.Current.Chrome.Reveal(ImGui.GetTime()); }
            }
            ImGui.EndChild();
            ImGui.SetCursorPos(cursor); ImGui.Dummy(Vector2.Zero);
        }
        finally { ImGui.PopStyleColor(3); ImGui.PopStyleVar(3); }
    }
}
