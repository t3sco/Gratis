using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Gratis.Classes;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed class MarkerBatch
{
    private struct Entry
    {
        public MarkerInfo Marker;
        public Vector2 Center;
        public int Rank, Root, Count, Next, Tail, CellNext;
    }
    private int Compare(int a, int b)
    {
        var rank = entries[a].Rank.CompareTo(entries[b].Rank);
        return rank != 0 ? rank : a.CompareTo(b);
    }
    private Entry[] entries = new Entry[4096];
    private int[] order = new int[4096];
    private readonly Dictionary<(int X, int Y), int> cells = new(4096);
    private readonly Comparison<int> comparer;
    private readonly StringBuilder memberText = new(512);
    private static readonly string[] Counts = BuildCounts();
    private int count;
    private bool collecting;
    public bool Enabled { get; private set; }
    public int Submitted => count;
    public int Hidden { get; private set; }
    public int Clusters { get; private set; }
    public int Representatives { get; private set; }
    public int DistanceChecks { get; private set; }

    public MarkerBatch() => comparer = Compare;

    private static string[] BuildCounts()
    {
        var values = new string[4097];
        for (var i = 0; i < values.Length; i++) values[i] = i.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
        return values;
    }

    public void Begin()
    {
        Array.Clear(entries, 0, count);
        count = Hidden = Clusters = Representatives = DistanceChecks = 0;
        cells.Clear();
        Enabled = MapDrawContext.Current.Config is { IsGratis: true, Markers.Declutter: true };
        collecting = Enabled;
    }

    public bool Enqueue(MarkerInfo marker)
    {
        if (!collecting) return false;
        if (count == entries.Length) {
            Array.Resize(ref entries, count * 2);
            Array.Resize(ref order, count * 2);
        }
        var category = FlatMarkerDrawing.Category(marker);
        var group = marker.IsTarget || category == MarkerCategory.Objective ? MarkerPriority.Objectives :
            category is MarkerCategory.Transport or MarkerCategory.Link ? MarkerPriority.Transport : MarkerPriority.Ambient;
        var priority = MapDrawContext.Current.Config.Markers.Priority;
        var rank = Array.IndexOf(priority, group);
        entries[count] = new Entry {
            Marker = marker, Center = marker.Position + marker.Offset,
            Rank = rank < 0 ? 2 : rank, Root = count, Count = 1, Next = -1, Tail = count, CellNext = -1,
        };
        order[count] = count;
        count++;
        return true;
    }

    public void Resolve(Vector2 frame)
    {
        var config = MapDrawContext.Current.Config.Markers;
        var spacing = Math.Clamp(config.ClusterDistance, 8, 64) * MapDrawContext.Current.GlobalScale;
        var square = spacing * spacing;
        var dense = count * square * 4 > frame.X * frame.Y;
        var veryDense = count * square > frame.X * frame.Y;
        order.AsSpan(0, count).Sort(comparer);
        for (var sorted = 0; sorted < count; sorted++) {
            var i = order[sorted];
            ref var entry = ref entries[i];
            if (config.HideLowPriority && dense && entry.Marker.Scale < .65f && entry.Rank > 0 &&
                (entry.Rank > 1 || veryDense && entry.Marker.Scale < .4f)) {
                entry.Root = -1;
                Hidden++;
                continue;
            }
            var x = (int)MathF.Floor(entry.Center.X / spacing);
            var y = (int)MathF.Floor(entry.Center.Y / spacing);
            var nearest = -1;
            var distance = square;
            for (var dy = -1; dy <= 1; dy++) {
                for (var dx = -1; dx <= 1; dx++) {
                    if (!cells.TryGetValue((x + dx, y + dy), out var index)) continue;
                    for (; index >= 0; index = entries[index].CellNext) {
                        DistanceChecks++;
                        var d = Vector2.DistanceSquared(entries[index].Center, entry.Center);
                        if (d < distance) { distance = d; nearest = index; }
                    }
                }
            }
            if (nearest >= 0) {
                ref var root = ref entries[nearest];
                entry.Root = nearest;
                entries[root.Tail].Next = i;
                root.Tail = i;
                root.Count++;
            }
            else {
                entry.CellNext = cells.TryGetValue((x, y), out var head) ? head : -1;
                cells[(x, y)] = i;
                Representatives++;
            }
        }
    }

    public void Flush()
    {
        collecting = false;
        if (!Enabled) return;
        Resolve(ImGui.GetWindowSize());
        for (var sorted = count - 1; sorted >= 0; sorted--) {
            var i = order[sorted];
            if (entries[i].Root != i) continue;
            if (entries[i].Count == 1) DrawHelpers.DrawPreparedMarker(entries[i].Marker);
            else { Clusters++; DrawCluster(i); }
        }
    }

    private void DrawCluster(int index)
    {
        ref var root = ref entries[index];
        var context = MapDrawContext.Current;
        var scale = context.GlobalScale;
        var label = root.Count < Counts.Length ? Counts[root.Count] : "4096+";
        var size = ImGui.CalcTextSize(label) + new Vector2(12, 6) * scale;
        var center = root.Center + ImGui.GetWindowPos();
        var half = size / 2;
        if (!MapShape.ContainsSymbol(center, half + new Vector2(scale)) ||
            !MapShape.Contains(center - half) || !MapShape.Contains(center + half) ||
            !MapShape.Contains(center + new Vector2(half.X, -half.Y)) ||
            !MapShape.Contains(center + new Vector2(-half.X, half.Y))) return;
        ImGui.SetCursorScreenPos(center - half);
        ImGui.Dummy(size);
        var list = ImGui.GetWindowDrawList();
        list.AddRectFilled(center - half, center + half, ImGui.GetColorU32(GratisTheme.Outline with { W = .94f }), 7 * scale);
        list.AddRect(center - half, center + half, ImGui.GetColorU32(context.Config.AccentColor), 7 * scale);
        list.AddText(center - half + new Vector2(6, 3) * scale, ImGui.GetColorU32(Vector4.One), label);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) context.ZoomCluster(root.Center, root.Marker.Scale);
        if (!ImGui.IsItemHovered()) return;
        memberText.Clear();
        var member = index;
        var listed = 0;
        while (member >= 0 && listed < 8) {
            if (listed > 0) memberText.Append('\n');
            var marker = entries[member].Marker;
            memberText.Append(marker.PrimaryText?.Invoke() is { Length: > 0 } text ? text : FlatMarkerDrawing.Label(FlatMarkerDrawing.Category(marker)));
            member = entries[member].Next;
            listed++;
        }
        if (root.Count > listed) memberText.Append('\n').Append('+').Append(root.Count - listed).Append(" more");
        HoverCardDrawing.Draw("marker-cluster", label + " markers · click to zoom", memberText.ToString());
    }
}
