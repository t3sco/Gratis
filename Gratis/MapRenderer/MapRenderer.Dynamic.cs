using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Gratis.Extensions;

namespace Gratis.MapRenderer;

public partial class MapRenderer {
    // Grouping state kept between frames: the LINQ GroupBy this replaced allocated a lookup, a grouping per data ID
    // and several delegates and enumerators every frame.
    private readonly Dictionary<(ushort DataId, Vector3 Position), int> dynamicGroupLookup = [];
    private readonly List<DynamicMarkerGroup> dynamicGroups = [];

    private struct DynamicMarkerGroup {
        public MapMarkerData First;
        public bool HasForegroundIcon;
        public uint ForegroundIconId;
        public float MaxRadius;
    }

    private unsafe void DrawDynamicMarkers() {
        // The game splits the radius and foreground icon into records with the same data ID.
        // Groups are collected in first-seen order (as GroupBy did) before anything is drawn.
        dynamicGroupLookup.Clear();
        dynamicGroups.Clear();
        foreach (ref readonly var marker in AgentMap.Instance()->EventMarkers.AsSpan()) {
            var key = (marker.DataId, marker.Position);
            if (!dynamicGroupLookup.TryGetValue(key, out var index)) {
                index = dynamicGroups.Count;
                dynamicGroupLookup[key] = index;
                dynamicGroups.Add(new DynamicMarkerGroup { First = marker, MaxRadius = marker.Radius });
            }

            ref var group = ref CollectionsMarshal.AsSpan(dynamicGroups)[index];
            if (!group.HasForegroundIcon && marker.IconId is not 60493) {
                group.HasForegroundIcon = true;
                group.ForegroundIconId = marker.IconId;
            }
            if (marker.Radius > group.MaxRadius)
                group.MaxRadius = marker.Radius;
        }

        for (var i = 0; i < dynamicGroups.Count; i++) {
            var group = dynamicGroups[i];
            var markerCopy = group.First;

            if (group.HasForegroundIcon)
                markerCopy.IconId = group.ForegroundIconId;

            markerCopy.Radius = group.MaxRadius;

            if (HousingManager.Instance()->CurrentTerritory is not null) {
                markerCopy.Radius = 0.0f;
            }

            markerCopy.Draw(DrawPosition, Scale);
        }
    }
}