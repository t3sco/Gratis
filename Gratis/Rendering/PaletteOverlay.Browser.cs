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

public sealed partial class PaletteOverlay {
    public Vector2 FirstCardCenter { get; private set; }
    private const int ZonePageSize = 4;
    internal const float NearbyCardHeight = 154, ExpansionCardHeight = 216;
    private readonly HashSet<uint> openZones = [];
    private readonly Dictionary<uint, int> zoneLimits = [];
    private bool resetBrowserScroll;
    private sealed record BrowserItem(PaletteZone Zone, PlaceResult? Result = null, bool More = false);

    private BrowserItem[] BrowserItems() {
        var items = new List<BrowserItem>();
        foreach (var zone in browserZones) {
            items.Add(new(zone));
            if (!openZones.Contains(zone.MapId))
                continue;
            var limit = zoneLimits.GetValueOrDefault(zone.MapId, ZonePageSize);
            items.AddRange(zone.Places.Take(limit).Select(r => new BrowserItem(zone, r)));
            if (zone.Places.Length > limit)
                items.Add(new(zone, More: true));
        }
        return items.ToArray();
    }

    private void UseBrowserItem(BrowserItem item, MapDrawingConfig config) {
        if (item.Result is { } result) {
            if (ImGui.GetIO().KeyAlt && config.PaletteTeleportButton)
                Teleport(result.Place, false);
            else
                Activate(result.Place);
        }
        else if (item.More)
            zoneLimits[item.Zone.MapId] = zoneLimits.GetValueOrDefault(item.Zone.MapId, ZonePageSize) + ZonePageSize;
        else if (!openZones.Remove(item.Zone.MapId))
            openZones.Add(item.Zone.MapId);
    }

    private void SetQuery(string text) {
        query = text;
        queryInputId = $"##place-query-{++openSerial}";
        focusSearch = true;
    }

    private void DrawBrowser(MapDrawingConfig config, float scale) {
        if (resetBrowserScroll && IsOpen) {
            ImGui.SetScrollY(0);
            resetBrowserScroll = false;
        }
        var width = ImGui.GetContentRegionAvail().X;
        var columns = expansion == null ? PaletteBrowser.Columns(width, scale) : 1;
        var items = expansion == null ? [] : BrowserItems();
        var count = expansion == null ? expansions.Length : items.Length;
        var horizontal = (IsOpen && ImGui.IsKeyPressed(ImGuiKey.RightArrow) ? 1 : 0)
            - (IsOpen && ImGui.IsKeyPressed(ImGuiKey.LeftArrow) ? 1 : 0);
        var vertical = (IsOpen && ImGui.IsKeyPressed(ImGuiKey.DownArrow) ? 1 : 0)
            - (IsOpen && ImGui.IsKeyPressed(ImGuiKey.UpArrow) ? 1 : 0);
        var previous = browserSelection;
        browserSelection = PaletteBrowser.Navigate(browserSelection, count, columns,
            expansion == null ? horizontal : 0, vertical);
        if (IsOpen && ImGui.IsKeyPressed(ImGuiKey.Home))
            browserSelection = 0;
        if (IsOpen && ImGui.IsKeyPressed(ImGuiKey.End))
            browserSelection = Math.Max(0, count - 1);
        if (IsOpen && ImGui.IsKeyPressed(ImGuiKey.PageDown))
            browserSelection = Math.Max(0, Math.Min(count - 1, browserSelection + columns * 2));
        if (IsOpen && ImGui.IsKeyPressed(ImGuiKey.PageUp))
            browserSelection = Math.Max(0, browserSelection - columns * 2);
        var keyboardMoved = previous != browserSelection;
        if (keyboardMoved)
            scrollSelection = true;
        if (expansion != null && IsOpen && ImGui.IsKeyPressed(ImGuiKey.Backspace, false)) {
            BrowseExpansion(null);
            return;
        }
        if (expansion != null && count > 0 && horizontal != 0) {
            var zone = items[browserSelection].Zone;
            if (horizontal > 0)
                openZones.Add(zone.MapId);
            else {
                openZones.Remove(zone.MapId);
                browserSelection = Array.FindIndex(items, item => item.Zone == zone);
            }
            items = BrowserItems();
            count = items.Length;
            keyboardMoved |= previous != browserSelection;
        }
        if (count > 0 && IsOpen && ImGui.IsKeyPressed(ImGuiKey.Enter, false)) {
            if (expansion == null)
                BrowseExpansion(expansions[browserSelection].Id);
            else
                UseBrowserItem(items[browserSelection], config);
            return;
        }

        if (expansion == null) {
            if (scope == 0) {
                Section("Nearby", scale, GratisIcons.LocateFixed);
                if (DrawNearby(config, scale, columns, width))
                    return;
                if (recent.Count > 0)
                    DrawRecent(config, scale);
            }
            Section("Explore the realms", scale, GratisIcons.Compass);
        }
        else {
            if (ImGui.SmallButton("< All expansions")) {
                BrowseExpansion(null);
                return;
            }
            Section(expansions.FirstOrDefault(e => e.Id == expansion)?.Name ?? "Places", scale, GratisIcons.Compass);
        }

        if (count == 0) {
            EmptyState.Draw(source.Building ? "Charting the realm..." : scope == 3
                ? "No tracked quests to browse." : "No charted places to browse.", EmptyFaces.PaletteBrowser);
            return;
        }
        if (expansion != null) {
            DrawBrowserZones(items, keyboardMoved, config, scale, width);
            return;
        }
        var start = ImGui.GetCursorScreenPos();
        var gap = GratisTheme.Gap * scale;
        var cardSize = new Vector2((width - (columns - 1) * gap) / columns, ExpansionCardHeight * scale);
        for (var i = 0; i < count; i++) {
            var position = start + new Vector2(i % columns * (cardSize.X + gap), i / columns * (cardSize.Y + gap));
            ImGui.SetCursorScreenPos(position);
            ImGui.PushID("browser-" + i);
            var clicked = ImGui.InvisibleButton("##card", cardSize);
            var hovered = ImGui.IsItemHovered();
            if (hovered && !keyboardMoved && ImGui.GetIO().MouseDelta.LengthSquared() > 0)
                browserSelection = i;
            if (keyboardMoved && i == browserSelection)
                UiMotion.ScrollToItem();
            if (i == 0)
                FirstCardCenter = position + cardSize / 2;
            var cover = expansions[i].Cover;
            if (ImGui.IsItemVisible()) {
                UiMotion.ItemId("##card");
                using var entrance = UiMotion.Surface("##card-enter", position, IsOpen, i < 8 ? i * .02f : 0);
                using var control = UiMotion.Control(ImGui.GetWindowDrawList(), position, position + cardSize);
                var subtitle = rowNames.Count(expansions[i].Places, scopes[scope]);
                var zones = scope == 2 ? "" : rowNames.Count(expansions[i].Zones, PlaceKind.Zone);
                DrawCard(cover, expansions[i].Name, "", ExpansionAccent(cover.Expansion), subtitle, position,
                    cardSize, i == browserSelection, config, scale, expansionCard: true, trailing: zones);
            }
            ImGui.PopID();
            if (clicked) {
                BrowseExpansion(expansions[i].Id);
                return;
            }
        }
        ImGui.SetCursorScreenPos(start + new Vector2(0, (count + columns - 1) / columns * (cardSize.Y + gap)));
        ImGui.Dummy(Vector2.Zero);

        if (scope == 0 && recent.Count == 0)
            DrawRecent(config, scale);
    }

    private void DrawBrowserZones(BrowserItem[] items, bool keyboardMoved, MapDrawingConfig config, float scale,
        float width) {
        var firstResult = true;
        for (var i = 0; i < items.Length; i++) {
            var item = items[i];
            if (item.Result is { } result) {
                scrollSelection = keyboardMoved;
                if (firstResult) {
                    FirstResultCenter = ImGui.GetCursorScreenPos() + new Vector2(width / 2, 38 * scale);
                    firstResult = false;
                }
                DrawRow(result, i, config, scale, width, browse: true);
                continue;
            }
            ImGui.PushID("zone-" + item.Zone.MapId + (item.More ? "-more" : ""));
            var expanded = openZones.Contains(item.Zone.MapId);
            var position = ImGui.GetCursorScreenPos();
            var clicked = ChromeControls.Button("##zone", new Vector2(width, 42 * scale));
            var list = ImGui.GetWindowDrawList();
            PaletteRowDrawing.Background(list, position, new(width, 42 * scale), config.AccentColor,
                i == browserSelection, ImGui.IsItemHovered(), scale);
            ChromeControls.Feedback(list, position, position + new Vector2(width, 42 * scale),
                fill: false, scale: scale);
            var label = item.More ? rowNames.Count(Math.Min(ZonePageSize, item.Zone.Places.Length
                - zoneLimits.GetValueOrDefault(item.Zone.MapId, ZonePageSize)), scopes[scope]) : item.Zone.Name;
            GratisIcons.Draw(list, position + new Vector2(16, 21) * scale,
                !item.More && expanded ? GratisIcons.Minus : GratisIcons.Plus,
                GratisTheme.Secondary, 12 * scale);
            PaletteRowDrawing.TextPair(list, position + new Vector2(32, 13) * scale, width - 44 * scale,
                label, item.More ? "Show more" : rowNames.Count(item.Zone.Places.Length, scopes[scope]), scale, rowNames);
            if (ImGui.IsItemHovered() && !keyboardMoved && ImGui.GetIO().MouseDelta.LengthSquared() > 0)
                browserSelection = i;
            if (keyboardMoved && i == browserSelection)
                UiMotion.ScrollToItem();
            ImGui.PopID();
            if (clicked) {
                browserSelection = i;
                UseBrowserItem(item, config);
                return;
            }
        }
    }

    // Each card carries the game's own marker icon, drawn large when the place has no art of its own.
    private static readonly (string Title, string Query, uint Icon)[] NearbyCards = [
        ("Nearest aetheryte", "aetheryte", 60453),
        ("Nearest market board", "market board", 60460),
        ("Nearest inn", "inn", 60436),
    ];

    private PlaceResult?[] FindNearby() => NearbyCards.Select(card => NearestPlaces.Find(
        source.Entries.Concat(MapDrawContext.Current.MarkerTargets.Entries), card.Query, context, source.Access,
        showUnattuned) is { Length: > 0 } found ? (PlaceResult?)found[0] : null).ToArray();

    private bool DrawNearby(MapDrawingConfig config, float scale, int columns, float width) {
        var start = ImGui.GetCursorScreenPos();
        var gap = GratisTheme.Gap * scale;
        var cardSize = new Vector2((width - (columns - 1) * gap) / columns, NearbyCardHeight * scale);
        for (var i = 0; i < NearbyCards.Length; i++) {
            var position = start + new Vector2(i % columns * (cardSize.X + gap), i / columns * (cardSize.Y + gap));
            ImGui.SetCursorScreenPos(position);
            ImGui.PushID("nearby-" + i);
            var clicked = ImGui.InvisibleButton("##card", cardSize);
            var hovered = ImGui.IsItemHovered();
            var found = nearby[i];
            if (ImGui.IsItemVisible()) {
                UiMotion.ItemId("##card");
                using var control = UiMotion.Control(ImGui.GetWindowDrawList(), position, position + cardSize);
                var text = found is { } result ? rowNames.Text(result, results) : default;
                DrawCard(found?.Place, found?.Place.Name ?? NearbyCards[i].Title,
                    found.HasValue ? NearbyCards[i].Title : "", GratisTheme.Transport,
                    found.HasValue ? text.Detail : "None in this zone", position, cardSize, hovered && found.HasValue, config, scale,
                    trailing: found.HasValue ? text.Distance : "", iconId: NearbyCards[i].Icon, slide: false);
            }
            ImGui.PopID();
            // A card with nothing to go to does nothing: searching for it would only show an empty list.
            if (!clicked || found is not { } target)
                continue;
            Activate(target.Place);
            return true;
        }
        ImGui.SetCursorScreenPos(start + new Vector2(0, (NearbyCards.Length + columns - 1) / columns * (cardSize.Y + gap)));
        ImGui.Dummy(Vector2.Zero);
        return false;
    }

    private void DrawRecent(MapDrawingConfig config, float scale) {
        Section("Recently visited", scale, GratisIcons.Repeat);
        var visited = source.Entries.Where(p => recent.ContainsKey(p.Id)).OrderByDescending(p =>
            recent[p.Id]).Take(2).ToArray();
        if (visited.Length == 0) {
            EmptyState.Draw("Your next destination starts a new journey.", EmptyFaces.PaletteRecent, scale);
        }
        foreach (var place in visited)
            DrawQuickPlace(new(place, 0, source.Access(place),
                place.MapId == context.MapId && context.Player is { } player && place.World is { } world
                    ? Vector2.Distance(player, world) : null),
                place.Name, config, scale);
        if (recentQueries.Count > 0) {
            Section("Recent searches", scale, GratisIcons.Search);
            foreach (var text in recentQueries.Take(3))
                if (ChromeControls.TextButton(text, new Vector2(-1, GratisTheme.ControlHeight * scale)))
                    SetQuery(text);
        }
    }

    private static void Section(string label, float scale, char icon) =>
        SectionHeader.Draw(label, SectionFaces.Palette, scale, icon);

    private void DrawQuickPlace(PlaceResult result, string title, MapDrawingConfig config, float scale) {
        var position = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.PushID("quick-" + result.Place.Id);
        if (ImGui.InvisibleButton("##quick", new Vector2(width, 60 * scale)))
            Activate(result.Place);
        var list = ImGui.GetWindowDrawList();
        if (ImGui.IsItemHovered())
            list.AddRectFilled(position, position + new Vector2(width, 60 * scale),
                ImGui.GetColorU32(config.AccentColor with { W = .1f }), GratisTheme.Radius.Control * scale);
        list.AddCircleFilled(position + new Vector2(22, 29) * scale, 19 * scale,
            ImGui.GetColorU32(GratisTheme.Transport with { W = .09f }));
        DrawKindIcon(result.Place, list, position + new Vector2(22, 29) * scale, 18 * scale);
        var text = rowNames.Text(result, results);
        PaletteRowDrawing.TextPair(list, position + new Vector2(52, 8) * scale, width - 64 * scale,
            title, text.Distance, scale, rowNames);
        PaletteRowDrawing.TextPair(list, position + new Vector2(52, 33) * scale, width - 64 * scale,
            text.Detail, text.Expansion, scale, rowNames, secondary: true);
        ImGui.PopID();
    }

    private void DrawCard(PlaceEntry? place, string title, string tag, Vector4 accent, string subtitle, Vector2 min,
        Vector2 size, bool active, MapDrawingConfig config, float scale, bool expansionCard = false, string trailing = "",
        uint iconId = 0, bool slide = true) {
        var list = ImGui.GetWindowDrawList();
        var max = min + size;
        if (place != null && (iconId == 0 || config.PaletteImagery && source.Image(place).Ready))
            DrawArt(place, min, size, config, scale, GratisTheme.Radius.Card * scale);
        else {
            var top = ImGui.GetColorU32(accent with { X = accent.X * .25f, Y = accent.Y * .25f, Z = accent.Z * .25f });
            var bottom = ImGui.GetColorU32(Vector4.Lerp(GratisTheme.Surface with { W = 1 }, accent, .4f));
            RoundedArtwork.Gradient(list, min, max, top, bottom, GratisTheme.Radius.Card * scale);
            if (iconId != 0 && MapDrawContext.Current.GetIcon(iconId) is { Size.X: > 0 } icon) {
                var extent = MathF.Min(size.X, size.Y) * .42f;
                var center = min + new Vector2(size.X / 2, size.Y * .38f);
                var half = new Vector2(extent) / 2;
                list.AddImage(icon.Handle, center - half, center + half);
            }
        }
        RoundedArtwork.Gradient(list, min + size * new Vector2(0, .22f), max, 0, 0xf5101012,
            GratisTheme.Radius.Card * scale, ImDrawFlags.RoundCornersBottom);
        if (tag.Length > 0)
            using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * scale, ChromeFontRole.Chip)?.Push()) {
                // A dark pill and white text: coloured text alone vanishes over busy artwork.
                var label = TextDrawing.Measure(tag);
                var pill = min + new Vector2(10, 10) * scale;
                var pillSize = label + new Vector2(16, 8) * scale;
                list.AddRectFilled(pill, pill + pillSize, ImGui.GetColorU32(new Vector4(0, 0, 0, .72f)),
                    GratisTheme.Radius.Pill(pillSize));
                TextDrawing.Draw(list, pill + new Vector2(8, 4) * scale, ImGui.GetColorU32(GratisTheme.Primary), tag);
            }
        // The name and its detail line form one tight group pinned bottom-left.
        float titleHeight, subtitleHeight;
        using (MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * scale, ChromeFontRole.Body)?.Push())
            titleHeight = ImGui.GetTextLineHeight();
        using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * scale, ChromeFontRole.Body)?.Push())
            subtitleHeight = ImGui.GetTextLineHeight();
        var subtitleTop = size.Y - 12 * scale - subtitleHeight;
        list.PushClipRect(min + new Vector2(12 * scale, 0), max - new Vector2(10 * scale, 0), true);
        var titleTop = subtitle.Length > 0 || trailing.Length > 0 ? subtitleTop - 2 * scale - titleHeight
            : size.Y - 12 * scale - titleHeight;
        PaletteRowDrawing.TextPair(list, min + new Vector2(12 * scale, titleTop), size.X - 24 * scale, title, "", scale,
            rowNames);
        PaletteRowDrawing.TextPair(list, min + new Vector2(12 * scale, subtitleTop),
            size.X - 24 * scale, subtitle, trailing, scale, rowNames, secondary: true,
            keepTrailing: !expansionCard);
        list.PopClipRect();
        if (expansionCard && place != null) {
            var crest = source.Crest(place);
            if (crest.Ready) {
                var crestSize = new Vector2(crest.Width, crest.Height)
                    * (28 * scale / Math.Max(crest.Width, crest.Height));
                // The crest sits above the name, which leaves the count free to align right.
                var crestMin = min + new Vector2(12 * scale, titleTop - 8 * scale - crestSize.Y);
                list.AddImage(crest.Handle, crestMin, crestMin + crestSize);
            }
        }
        var opacity = UiMotion.Value(ImGui.GetID("##browser-accent"), 7, active ? 1 : 0, initial: 0);
        if (opacity > .001f) {
            // Only the keyboard-selected card slides; a hovered card is outlined where it is. The slide shares one
            // animated position, so two active cards would drag each other's outline away.
            var indicator = active && slide ? UiMotion.Selection(selectionMotionKey, min) : min;
            list.AddRect(indicator, indicator + size, ImGui.GetColorU32(accent with { W = opacity }),
                GratisTheme.Radius.Card * scale, 0, 1.5f * scale);
        }
    }

    public static Vector4 ExpansionAccent(uint id) => id switch {
        0 => GratisTheme.WarmAccent,
        1 => GratisTheme.Crystal,
        2 => new(.85f, .24f, .30f, 1),
        3 => GratisTheme.Link,
        4 => Vector4.Lerp(GratisTheme.Service, GratisTheme.Transport, .5f),
        5 => GratisTheme.Objective,
        _ => GratisTheme.Transport,
    };

    private void DrawArt(PlaceEntry place, Vector2 min, Vector2 size, MapDrawingConfig config, float scale, float radius) {
        var list = ImGui.GetWindowDrawList();
        var image = config.PaletteImagery ? source.Image(place) : default;
        if (image.Ready) {
            var sourceAspect = (float)image.Width / image.Height;
            var targetAspect = size.X / size.Y;
            var uvSize = sourceAspect > targetAspect ? new Vector2(targetAspect / sourceAspect, 1)
                : new Vector2(1, sourceAspect / targetAspect);
            var uvMin = (Vector2.One - uvSize) / 2;
            list.AddImageRounded(image.Handle, min, min + size, uvMin, uvMin + uvSize, 0xffffffff, radius);
        }
        else {
            var accent = ExpansionAccent(place.Expansion);
            var top = ImGui.GetColorU32(accent with { X = accent.X * .25f, Y = accent.Y * .25f, Z = accent.Z * .25f });
            var bottom = ImGui.GetColorU32(Vector4.Lerp(GratisTheme.Surface with { W = 1 }, accent, .4f));
            RoundedArtwork.Gradient(list, min, min + size, top, bottom, radius);
            if (config.PaletteImagery) {
                var seed = image.Seed != 0 ? image.Seed : place.MapId + place.Expansion * 31;
                list.PushClipRect(min, min + size, true);
                list.AddCircleFilled(min + size * new Vector2(.75f, .28f), MathF.Min(size.X, size.Y) * .12f,
                    ImGui.GetColorU32(accent with { W = .22f }), 32);
                for (var ridge = 0; ridge < 7; ridge++) {
                    var x = min.X + size.X * (ridge - 1) / 5;
                    var y = min.Y + size.Y * (.28f + ((seed + (uint)ridge * 17) % 31) / 100f);
                    RoundedArtwork.Triangle(list, min, min + size, radius,
                        new(x - size.X * .35f, min.Y + size.Y), new(x + size.X * .16f, y),
                        new(x + size.X * .65f, min.Y + size.Y),
                        ImGui.GetColorU32(Vector4.Lerp(accent, GratisTheme.Surface with { W = 1 },
                            .60f + ridge * .04f)));
                }
                list.PopClipRect();
            }
        }
        DrawKindIcon(place, list, min + new Vector2(size.X - 18 * scale, 19 * scale), 15 * scale);
    }

    private static char KindIcon(PlaceEntry place) => place.Kind switch {
        PlaceKind.Aetheryte => place.IsShard ? GratisIcons.Repeat : GratisIcons.Sparkles,
        PlaceKind.Zone => GratisIcons.Compass,
        PlaceKind.Quest => GratisIcons.BookOpen,
        PlaceKind.Flag => GratisIcons.Flag,
        PlaceKind.Pin => GratisIcons.Anchor,
        PlaceKind.Action => GratisIcons.LocateFixed,
        _ => GratisIcons.MapPin,
    };

    private static void DrawKindIcon(PlaceEntry place, ImDrawListPtr list, Vector2 center, float size) {
        if (place.Kind == PlaceKind.Quest) {
            var gold = ImGui.GetColorU32(GratisTheme.Objective);
            list.AddQuad(center - new Vector2(0, size * .52f), center + new Vector2(size * .4f, 0),
                center + new Vector2(0, size * .52f), center - new Vector2(size * .4f, 0), gold, 1.2f);
            list.AddLine(center - new Vector2(0, size * .23f), center + new Vector2(0, size * .08f), gold, 1.5f);
            list.AddCircleFilled(center + new Vector2(0, size * .25f), 1.1f, gold, 8);
            return;
        }
        if (place.Kind != PlaceKind.Aetheryte) {
            GratisIcons.Draw(list, center, KindIcon(place), GratisTheme.Primary, size);
            return;
        }
        var colour = ImGui.GetColorU32(GratisTheme.Transport);
        var top = center - new Vector2(0, size * .55f);
        var bottom = center + new Vector2(0, size * .55f);
        var left = center - new Vector2(size * .27f, 0);
        var right = center + new Vector2(size * .27f, 0);
        list.AddQuadFilled(top, right, bottom, left, ImGui.GetColorU32(GratisTheme.Transport with { W = .18f }));
        list.AddQuad(top, right, bottom, left, colour, 1.1f);
        list.AddLine(top, bottom, colour);
        list.AddLine(left, center + new Vector2(0, size * .15f), colour);
        list.AddLine(right, center + new Vector2(0, size * .15f), colour);
    }
}
