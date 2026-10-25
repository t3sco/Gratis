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

public sealed class MarkerBatch {
    private struct Entry {
        public MarkerInfo Marker;
        public Vector2 Center, HalfSize;
        public MarkerCategory Category;
        public bool Protected;
        public int Root, Count, Next, Tail, CellNext;
    }
    private Entry[] entries = new Entry[4096];
    private readonly Dictionary<(MarkerCategory Category, int X, int Y), int> cells = new(4096);
    private readonly Dictionary<(int X, int Y), int> protectedCells = new(4096);
    private readonly StringBuilder memberText = new(512);
    private static readonly string[] Counts = BuildCounts();
    private int count;
    private bool collecting;
    private Vector2 maximumProtectedHalfSize;
    private float protectedSpacing;
    private Vector2 BadgeHalfSize => new Vector2(48, 18) * MapDrawContext.Current.GlobalScale;
    public bool Enabled { get; private set; }
    public int Submitted => count;
    public int Protected { get; private set; }
    public int Hidden => 0;
    public int Clusters { get; private set; }
    public int Representatives { get; private set; }
    public int DistanceChecks { get; private set; }
    public int DissolvedClusters { get; private set; }

    internal bool IsStandalone(int index) => entries[index].Root == index && entries[index].Count == 1;
    internal int RootOf(int index) => entries[index].Root;
    internal int CountAt(int index) => entries[index].Count;
    internal bool ClusterOverlapsProtected(int index) => OverlapsProtected(entries[index].Center, BadgeHalfSize);

    private static string[] BuildCounts() {
        var values = new string[4097];
        for (var i = 0; i < values.Length; i++)
            values[i] = i.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
        return values;
    }

    public void Begin() {
        Array.Clear(entries, 0, count);
        count = Protected = Clusters = Representatives = DistanceChecks = DissolvedClusters = 0;
        cells.Clear();
        protectedCells.Clear();
        maximumProtectedHalfSize = Vector2.Zero;
        protectedSpacing = 64 * MapDrawContext.Current.GlobalScale;
        Enabled = MapDrawContext.Current.Config is { IsGratis: true, Markers.Declutter: true };
        collecting = Enabled;
    }

    public bool Enqueue(MarkerInfo marker) {
        if (!collecting)
            return false;
        if (MapDrawContext.Current.Visibility.State(MarkerVisibility.Group(marker),
            MarkerVisibility.IsPriority(marker)) != MarkerVisibilityState.Normal)
            return false;
        if (count == entries.Length)
            Array.Resize(ref entries, count * 2);
        var keep = MarkerRelevance.IsProtected(marker) || MapDrawContext.Current.Spotlight?.Matches(marker) == true;
        entries[count] = new Entry {
            Marker = marker, Center = marker.Position + marker.Offset,
            HalfSize = keep ? MarkerVisual.HalfSize(marker) : Vector2.Zero,
            Category = FlatMarkerDrawing.Category(marker), Protected = keep,
            Root = count, Count = 1, Next = -1, Tail = count, CellNext = -1,
        };
        if (keep)
            Protected++;
        count++;
        return true;
    }

    public void Resolve() {
        var spacing = Math.Clamp(MapDrawContext.Current.Config.Markers.ClusterDistance, 8, 64)
            * MapDrawContext.Current.GlobalScale;
        var square = spacing * spacing;
        for (var i = 0; i < count; i++) {
            ref var entry = ref entries[i];
            if (!entry.Protected)
                continue;
            var cell = ((int)MathF.Floor(entry.Center.X / protectedSpacing),
                (int)MathF.Floor(entry.Center.Y / protectedSpacing));
            entry.CellNext = protectedCells.TryGetValue(cell, out var head) ? head : -1;
            protectedCells[cell] = i;
            maximumProtectedHalfSize = Vector2.Max(maximumProtectedHalfSize, entry.HalfSize);
            Representatives++;
        }
        for (var i = 0; i < count; i++) {
            ref var entry = ref entries[i];
            if (entry.Protected)
                continue;
            var x = (int)MathF.Floor(entry.Center.X / spacing);
            var y = (int)MathF.Floor(entry.Center.Y / spacing);
            var nearest = -1;
            var distance = square;
            for (var dy = -1; dy <= 1; dy++) {
                for (var dx = -1; dx <= 1; dx++) {
                    if (!cells.TryGetValue((entry.Category, x + dx, y + dy), out var index))
                        continue;
                    for (; index >= 0; index = entries[index].CellNext) {
                        DistanceChecks++;
                        var d = Vector2.DistanceSquared(entries[index].Center, entry.Center);
                        if (d < distance) {
                            distance = d;
                            nearest = index;
                        }
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
                var cell = (entry.Category, x, y);
                entry.CellNext = cells.TryGetValue(cell, out var head) ? head : -1;
                cells[cell] = i;
                Representatives++;
            }
        }
        for (var i = 0; i < count; i++) {
            if (entries[i].Root != i || entries[i].Count < 2 || !OverlapsProtected(entries[i].Center, BadgeHalfSize))
                continue;
            Representatives += entries[i].Count - 1;
            DissolvedClusters++;
            var member = i;
            while (member >= 0) {
                ref var entry = ref entries[member];
                var next = entry.Next;
                entry.Root = entry.Tail = member;
                entry.Count = 1;
                entry.Next = -1;
                member = next;
            }
        }
    }

    private bool OverlapsProtected(Vector2 center, Vector2 half) {
        if (Protected == 0)
            return false;
        var reach = half + maximumProtectedHalfSize;
        var min = (center - reach) / protectedSpacing;
        var max = (center + reach) / protectedSpacing;
        for (var y = (int)MathF.Floor(min.Y); y <= (int)MathF.Floor(max.Y); y++) {
            for (var x = (int)MathF.Floor(min.X); x <= (int)MathF.Floor(max.X); x++) {
                if (!protectedCells.TryGetValue((x, y), out var index))
                    continue;
                for (; index >= 0; index = entries[index].CellNext) {
                    var delta = Vector2.Abs(entries[index].Center - center);
                    var bounds = half + entries[index].HalfSize;
                    if (delta.X <= bounds.X && delta.Y <= bounds.Y)
                        return true;
                }
            }
        }
        return false;
    }

    public void Flush() {
        collecting = false;
        if (!Enabled)
            return;
        Resolve();
        for (var i = 0; i < count; i++) {
            if (entries[i].Protected || entries[i].Root != i)
                continue;
            if (entries[i].Count == 1)
                DrawHelpers.DrawPreparedMarker(entries[i].Marker);
            else
                DrawCluster(i);
        }
        for (var i = 0; i < count; i++)
            if (entries[i].Protected)
                DrawHelpers.DrawPreparedMarker(entries[i].Marker);
    }

    private void DrawCluster(int index) {
        ref var root = ref entries[index];
        var context = MapDrawContext.Current;
        var scale = context.GlobalScale;
        var label = root.Count < Counts.Length ? Counts[root.Count] : "4096+";
        using var font = context.GetChromeFont(11 * scale, ChromeFontRole.Chip)?.Push();
        var size = TextDrawing.Measure(label) + new Vector2(12, 6) * scale;
        var center = root.Center + ImGui.GetWindowPos();
        var half = size / 2;
        if (OverlapsProtected(root.Center, half + new Vector2(scale))) {
            for (var member = index; member >= 0; member = entries[member].Next)
                DrawHelpers.DrawPreparedMarker(entries[member].Marker);
            return;
        }
        if (!MapShape.ContainsSymbol(center, half + new Vector2(scale)) || !MapShape.Contains(center - half)
            || !MapShape.Contains(center + half) || !MapShape.Contains(center + new Vector2(half.X, -half.Y))
            || !MapShape.Contains(center + new Vector2(-half.X, half.Y)))
            return;
        Clusters++;
        var spotlightOpacity = context.Spotlight?.MarkerAlpha(root.Marker) ?? 1;
        using var spotlightAlpha = FrameScopes.PushStyle(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * spotlightOpacity,
            spotlightOpacity < 1);
        ImGui.SetCursorScreenPos(center - half);
        ImGui.Dummy(size);
        var list = ImGui.GetWindowDrawList();
        list.AddRectFilled(center - half, center + half, ImGui.GetColorU32(GratisTheme.Outline with { W = .94f }),
            7 * scale);
        list.AddRect(center - half, center + half, ImGui.GetColorU32(context.Config.AccentColor), 7 * scale);
        TextDrawing.Draw(list, center - half + new Vector2(6, 3) * scale, ImGui.GetColorU32(Vector4.One), label);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            context.ZoomCluster(root.Center, root.Marker.Scale);
        if (!ImGui.IsItemHovered())
            return;
        memberText.Clear();
        var item = index;
        var listed = 0;
        while (item >= 0 && listed < 8) {
            if (listed > 0)
                memberText.Append('\n');
            var marker = entries[item].Marker;
            memberText.Append(marker.PrimaryText?.Invoke() is { Length: > 0 } text ? text
                : FlatMarkerDrawing.Label(FlatMarkerDrawing.Category(marker)));
            item = entries[item].Next;
            listed++;
        }
        if (root.Count > listed)
            memberText.Append('\n').Append('+').Append(root.Count - listed).Append(" more");
        HoverCardDrawing.Draw("marker-cluster", label + " ambient markers · click to zoom", memberText.ToString());
    }
}