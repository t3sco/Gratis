using System;
using System.Collections.Generic;
using System.Numerics;
using Gratis.Classes;

namespace Gratis.Rendering;

public partial class MapDrawingConfig
{
    public bool GratisContextMenu = true;
}

public sealed record ContextMenuItem(string Label, char Icon, Action? Invoke = null,
    IReadOnlyList<ContextMenuItem>? Children = null);

public interface IContextMenuActions
{
    string MapName { get; }
    Vector2 Coordinates(Vector2 world);
    void SetFlag(Vector2 world);
    string MapLink(Vector2 world);
    IReadOnlyList<ContextMenuItem> RecentFlags { get; }
    IReadOnlyList<ContextMenuItem> Layers { get; }
    void CaptureKeys();
}

public interface IContextMenuMeasure
{
    void Begin(uint mapId, Vector2 world);
}

public sealed partial class ContextMenu
{
    public IContextMenuActions? Actions { get; set; }
    public IContextMenuMeasure? Measure { get; set; }
    public string LastAction { get; private set; } = "none";
    public Vector2 World { get; private set; }
    public MarkerInfo? Marker { get; private set; }
    private string details = "";
    private readonly List<ContextMenuItem> items = [];
    private IReadOnlyList<ContextMenuItem> current = [];

    private void Build(IMapWindowHost host)
    {
        items.Clear(); details = "";
        var actions = Actions;
        void Add(string label, char icon, Action invoke) => items.Add(new(label, icon, invoke));
        void CopyCoordinates() => Copy(CoordinateShare.Text(actions?.Coordinates(World) ?? World,
            ImGuiShift() ? actions?.MapName : null));
        void Spotlight() => host.Renderer.BeginSpotlight(new(World, host.Renderer.ViewedMapId,
            SpotlightSource.Search, IncludePlayer: true));
        if (Marker is { } marker) {
            Add("Spotlight", GratisIcons.Sparkles, Spotlight);
            if (actions != null) Add("Set flag on it", GratisIcons.Flag, () => actions.SetFlag(World));
            Add("Copy name", GratisIcons.Copy, () => Copy(marker.PrimaryText?.Invoke() ?? FlatMarkerDrawing.Label(marker)));
            Add("Copy coordinates", GratisIcons.Copy, CopyCoordinates);
            var group = MarkerVisibility.Group(marker);
            var mute = new ContextMenuItem("Confirm hide", GratisIcons.EyeOff,
                () => MapDrawContext.Current.Visibility.Toggle(group));
            var priority = MarkerVisibility.IsPriority(marker);
            items.Add(new("Mute this marker group", GratisIcons.EyeOff,
                priority ? null : mute.Invoke,
                priority ? [new(group is MarkerGroup.Quests or MarkerGroup.SideQuests ? "Hide quests?" : $"Hide {group}?", GratisIcons.Info), mute] : null));
            Add("Show hover details", GratisIcons.Info, () => details =
                (marker.PrimaryText?.Invoke() ?? FlatMarkerDrawing.Label(marker)) + "\n" + marker.SecondaryText?.Invoke());
        }
        else {
            if (actions != null) Add("Set flag here", GratisIcons.Flag, () => actions.SetFlag(World));
            if (host.Config.Pins && MapDrawContext.Current.Assistance?.PinLayer is { } pins)
                Add("Add pin here", GratisIcons.MapPin, () => pins.Edit(host.Renderer.ViewedMapId, World));
            Add("Copy coordinates", GratisIcons.Copy, CopyCoordinates);
            if (actions != null) Add("Copy map link text", GratisIcons.Copy, () => Copy(actions.MapLink(World)));
            if (MapDrawContext.Current.Assistance is { } assistance) {
                Add("Show nearest aetheryte", GratisIcons.LocateFixed, () => {
                    Spotlight();
                    assistance.OfferContextTarget(new(World, host.Renderer.ViewedMapId, SpotlightSource.Search), host.Now);
                    var nearest = AetheryteSelection.Nearest(assistance.Places.Entries,
                        host.Renderer.ViewedMapId, World, assistance.Places.Access,
                        assistance.Provider.ActiveAetheryte, assistance.Provider.Available);
                    if (nearest is { } choice) assistance.HoverPlace(choice.Destination, anchor);
                });
            }
            if (Measure != null) Add("Measure from here", GratisIcons.Crosshair, () => Measure.Begin(host.Renderer.ViewedMapId, World));
            if (actions?.RecentFlags is { Count: > 0 } flags) items.Add(new("Recent flags", GratisIcons.Flag, Children: flags));
            if (actions?.Layers is { Count: > 0 } layers) items.Add(new("Layers", GratisIcons.Layers, Children: layers));
        }
        current = items; selected = 0;
    }
}
