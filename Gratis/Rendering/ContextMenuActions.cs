using System;
using System.Collections.Generic;
using System.Numerics;
using Gratis.Classes;

namespace Gratis.Rendering;

public sealed record ContextMenuItem(string Label, char Icon, Action? Invoke = null,
    IReadOnlyList<ContextMenuItem>? Children = null, bool KeepOpen = false, UnavailableAction Unavailable = UnavailableAction.None);

public interface IContextMenuActions {
    string MapName { get; }
    Vector2 Coordinates(Vector2 world);
    void SetFlag(Vector2 world);
    bool HasFlag { get; }
    void RemoveFlag();
    string MapLink(Vector2 world);
    IReadOnlyList<ContextMenuItem> RecentFlags { get; }
    IReadOnlyList<ContextMenuItem> Layers { get; }
    void CaptureKeys();
}

public interface IContextMenuMeasure {
    void Begin(uint mapId, Vector2 world);
}

public sealed partial class ContextMenu {
    public IContextMenuActions? Actions { get; set; }
    public IContextMenuMeasure? Measure { get; set; }
    public string LastAction { get; private set; } = "none";
    public Vector2 World { get; private set; }
    public MarkerInfo? Marker { get; private set; }
    private readonly List<ContextMenuItem> items = [];
    private IReadOnlyList<ContextMenuItem> current = [];

    private void Build(IMapWindowHost host) {
        items.Clear();
        var actions = Actions;
        void Add(string label, char icon, Action invoke) => items.Add(new(label, icon, invoke));
        void CopyCoordinates() =>
            Copy(CoordinateShare.Text(actions?.Coordinates(World) ?? World, ImGuiShift() ? actions?.MapName : null));
        void Spotlight() => host.Renderer.BeginSpotlight(new(World, host.Renderer.ViewedMapId, SpotlightSource.Search,
            IncludePlayer: true));
        if (Marker is { } marker) {
            Add("Spotlight", GratisIcons.Sparkles, Spotlight);
            if (host.Config.TravelActions && MapDrawContext.Current.Assistance is { } objectiveTravel
                && (marker.IsObjective || MarkerIcon.Family(marker) is MarkerIconFamily.Quest or MarkerIconFamily.Leve))
                Add("Show nearest aetheryte", GratisIcons.Compass,
                    () => objectiveTravel.OfferContextTarget(new(World, host.Renderer.ViewedMapId,
                        SpotlightSource.Quest, Icon: marker.IconId),
                        host.Now));
            if (actions != null && marker.IsFlag)
                Add("Remove flag", GratisIcons.Flag, actions.RemoveFlag);
            else if (actions != null)
                Add("Set flag here", GratisIcons.Flag, () => actions.SetFlag(World));
            Add("Copy name", GratisIcons.Copy,
                () => Copy(marker.PrimaryText?.Invoke() ?? FlatMarkerDrawing.Label(marker)));
            Add("Copy coordinates", GratisIcons.Copy, CopyCoordinates);
            var group = MarkerVisibility.Group(marker);
            void Mute() => MapDrawContext.Current.Visibility.Toggle(group);
            if (!MarkerVisibility.IsPriority(marker))
                Add("Mute this marker group", GratisIcons.EyeOff, Mute);
            else {
                // Priority groups arm in place: the same row turns into the confirmation, no submenu.
                var index = items.Count;
                var name = group is MarkerGroup.Quests or MarkerGroup.SideQuests ? "quests" : group.ToString();
                items.Add(new("Mute this marker group", GratisIcons.EyeOff, () => {
                    items[index] = new("Confirm hide " + name, GratisIcons.EyeOff, Mute);
                }, KeepOpen: true));
            }
        }
        else {
            if (actions != null)
                Add("Set flag here", GratisIcons.Flag, () => actions.SetFlag(World));
            if (actions is { HasFlag: true })
                Add("Remove flag", GratisIcons.Flag, actions.RemoveFlag);
            if (host.Config.Pins && MapDrawContext.Current.Assistance?.PinLayer is { } pins)
                Add("Add pin here", GratisIcons.MapPin, () => pins.Edit(host.Renderer.ViewedMapId, World));
            Add("Copy coordinates", GratisIcons.Copy, CopyCoordinates);
            if (actions != null)
                Add("Copy map link text", GratisIcons.MessageSquare, () => Copy(actions.MapLink(World)));
            if (host.Config.TravelActions && MapDrawContext.Current.Assistance is { } assistance) {
                Add("Show nearest aetheryte", GratisIcons.Compass, () => {
                    Spotlight();
                    assistance.OfferContextTarget(new(World, host.Renderer.ViewedMapId, SpotlightSource.Search),
                        host.Now);
                });
            }
            if (Measure != null)
                Add("Measure from here", GratisIcons.Crosshair, () => Measure.Begin(host.Renderer.ViewedMapId, World));
            if (actions?.RecentFlags is { Count: > 0 } flags)
                items.Add(new("Recent flags", GratisIcons.Flag, Children: flags));
            if (actions?.Layers is { Count: > 0 } layers)
                items.Add(new("Layers", GratisIcons.Layers, Children: layers));
        }
        if (Marker == null) {
            var presets = new List<ContextMenuItem>();
            foreach (var item in MapDrawContext.Current.WindowPresets.Menu(host.Config))
                presets.Add(item.Invoke == null && item.Children == null
                    ? item with { Unavailable = UnavailableAction.PresetsFull } : item);
            items.Add(new("Window presets", GratisIcons.LayoutGrid, Children: presets));
        }
        current = items;
        selected = -1;
    }
}