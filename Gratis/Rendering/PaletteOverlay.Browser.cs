using System;
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

    private void SetQuery(string text) {
        query = text;
        queryInputId = $"##place-query-{++openSerial}";
        focusSearch = true;
    }

    private void DrawBrowser(MapDrawingConfig config, float scale) {
        var width = ImGui.GetContentRegionAvail().X;
        var columns = PaletteBrowser.Columns(width, scale);
        var count = expansion == null ? expansions.Length : results.Length;
        var horizontal = (IsOpen && ImGui.IsKeyPressed(ImGuiKey.RightArrow) ? 1 : 0)
            - (IsOpen && ImGui.IsKeyPressed(ImGuiKey.LeftArrow) ? 1 : 0);
        var vertical = (IsOpen && ImGui.IsKeyPressed(ImGuiKey.DownArrow) ? 1 : 0)
            - (IsOpen && ImGui.IsKeyPressed(ImGuiKey.UpArrow) ? 1 : 0);
        var previous = browserSelection;
        browserSelection = PaletteBrowser.Navigate(browserSelection, count, columns, horizontal, vertical);
        if (IsOpen && ImGui.IsKeyPressed(ImGuiKey.Home))
            browserSelection = 0;
        if (IsOpen && ImGui.IsKeyPressed(ImGuiKey.End))
            browserSelection = Math.Max(0, count - 1);
        if (IsOpen && ImGui.IsKeyPressed(ImGuiKey.PageDown))
            browserSelection = Math.Min(count - 1, browserSelection + columns * 2);
        if (IsOpen && ImGui.IsKeyPressed(ImGuiKey.PageUp))
            browserSelection = Math.Max(0, browserSelection - columns * 2);
        var keyboardMoved = previous != browserSelection;
        if (keyboardMoved)
            scrollSelection = true;
        if (expansion != null && IsOpen && ImGui.IsKeyPressed(ImGuiKey.Backspace, false)) {
            BrowseExpansion(null);
            return;
        }
        if (count > 0 && IsOpen && ImGui.IsKeyPressed(ImGuiKey.Enter, false)) {
            if (expansion == null)
                BrowseExpansion(expansions[browserSelection].Id);
            else if (ImGui.GetIO().KeyAlt && config.PaletteTeleportButton)
                Teleport(results[browserSelection].Place, false);
            else
                Activate(results[browserSelection].Place);
            return;
        }

        if (expansion == null) {
            Section("Nearby", scale, GratisIcons.LocateFixed);
            if (DrawNearby(config, scale, columns, width))
                return;
            if (recent.Count > 0)
                DrawRecent(config, scale);
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
            ImGui.TextWrapped(source.Building ? "Charting the realm..." : "No charted places in this expansion yet.");
            return;
        }
        var start = ImGui.GetCursorScreenPos();
        var gap = GratisTheme.Gap * scale;
        var cardSize = new Vector2((width - (columns - 1) * gap) / columns, (expansion == null ? 154 : 164) * scale);
        for (var i = 0; i < count; i++) {
            var position = start + new Vector2(i % columns * (cardSize.X + gap), i / columns * (cardSize.Y + gap));
            ImGui.SetCursorScreenPos(position);
            ImGui.PushID("browser-" + i);
            var clicked = ImGui.InvisibleButton("##card", cardSize);
            var hovered = ImGui.IsItemHovered();
            if (hovered && ImGui.GetIO().MouseDelta.LengthSquared() > 0)
                browserSelection = i;
            if (keyboardMoved && i == browserSelection)
                UiMotion.ScrollToItem();
            if (i == 0)
                FirstCardCenter = position + cardSize / 2;
            var cover = expansion == null ? expansions[i].Cover : results[i].Place;
            if (ImGui.IsItemVisible()) {
                UiMotion.ItemId("##card");
                using var entrance = UiMotion.Surface("##card-enter", position, IsOpen, i < 8 ? i * .02f : 0);
                using var control = UiMotion.Control(ImGui.GetWindowDrawList(), position, position + cardSize);
                var title = expansion == null ? expansions[i].Name : PaletteBrowser.Name(results[i], results);
                var subtitle = expansion == null
                    ? $"{expansions[i].Zones} zone{(expansions[i].Zones == 1 ? "" : "s")} · "
                        + $"{expansions[i].Places} places"
                    : cover.Kind == PlaceKind.Aetheryte ? results[i].Access.Attuned ? "AETHERYTE · Attuned"
                    : "AETHERYTE · Not yet attuned" : "ZONE · " + cover.Subtitle.Split('>').Last().Trim();
                DrawCard(cover, title, expansion == null ? "EXPANSION " + (cover.Expansion + 1).ToString("00")
                    : cover.Kind == PlaceKind.Zone ? "REGION MAP" : "AETHERYTE", ExpansionAccent(cover.Expansion),
                    subtitle, position, cardSize, i == browserSelection, config, scale);
            }
            ImGui.PopID();
            if (clicked) {
                if (expansion == null)
                    BrowseExpansion(expansions[i].Id);
                else
                    Activate(results[i].Place);
                return;
            }
        }
        ImGui.SetCursorScreenPos(start + new Vector2(0, (count + columns - 1) / columns * (cardSize.Y + gap)));
        ImGui.Dummy(Vector2.Zero);

        if (expansion == null && recent.Count == 0)
            DrawRecent(config, scale);
    }

    private static readonly (string Title, string Query)[] NearbyCards = [
        ("Nearest aetheryte", "aetheryte"),
        ("Nearest market board", "market board"),
        ("Nearest inn", "inn"),
    ];

    private PlaceResult?[] FindNearby() => NearbyCards.Select(card => NearestPlaces.Find(
        source.Entries.Concat(MapDrawContext.Current.MarkerTargets.Entries), card.Query, context, source.Access,
        showUnattuned) is { Length: > 0 } found ? (PlaceResult?)found[0] : null).ToArray();

    private bool DrawNearby(MapDrawingConfig config, float scale, int columns, float width) {
        var start = ImGui.GetCursorScreenPos();
        var gap = GratisTheme.Gap * scale;
        var cardSize = new Vector2((width - (columns - 1) * gap) / columns, 154 * scale);
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
                var subtitle = found is { } result
                    ? (result.Distance is { } distance ? $"{distance:0}y · " : "") + result.Place.Subtitle.Split('>').Last().Trim()
                    : "Search this zone";
                DrawCard(found?.Place, NearbyCards[i].Title, "NEARBY", GratisTheme.Transport, subtitle, position,
                    cardSize, hovered, config, scale);
            }
            ImGui.PopID();
            if (!clicked)
                continue;
            if (found is { } target)
                Activate(target.Place);
            else
                SetQuery("nearest " + NearbyCards[i].Query);
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
            using var small = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * scale,
                ChromeFontRole.Body)?.Push();
            ImGui.TextDisabled("Your next destination starts a new journey.");
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

    private static void Section(string label, float scale, char icon) {
        ImGui.Spacing();
        var position = ImGui.GetCursorScreenPos();
        GratisIcons.Draw(ImGui.GetWindowDrawList(), position + new Vector2(8, 9) * scale, icon,
            GratisTheme.WarmAccent with { W = .75f }, 14 * scale);
        ImGui.SetCursorScreenPos(position + new Vector2(24 * scale, 0));
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * scale,
            ChromeFontRole.Body)?.Push();
        ImGui.TextUnformatted(label);
    }

    private void DrawQuickPlace(PlaceResult result, string title, MapDrawingConfig config, float scale) {
        var position = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.PushID("quick-" + result.Place.Id);
        if (ImGui.InvisibleButton("##quick", new Vector2(width, 60 * scale)))
            Activate(result.Place);
        var list = ImGui.GetWindowDrawList();
        if (ImGui.IsItemHovered())
            list.AddRectFilled(position, position + new Vector2(width, 60 * scale),
                ImGui.GetColorU32(config.AccentColor with { W = .1f }), GratisTheme.ButtonRadius * scale);
        list.AddCircleFilled(position + new Vector2(22, 29) * scale, 19 * scale,
            ImGui.GetColorU32(GratisTheme.Transport with { W = .09f }));
        DrawKindIcon(result.Place, list, position + new Vector2(22, 29) * scale, 18 * scale);
        list.PushClipRect(position + new Vector2(50 * scale, 0), position + new Vector2(width, 60 * scale), true);
        using (MapDrawContext.Current.GetChromeFont((GratisTheme.TextSize + 2) * scale, ChromeFontRole.Title)?.Push())
            TextDrawing.Draw(list, position + new Vector2(52, 8) * scale, ImGui.GetColorU32(GratisTheme.Primary),
                title);
        using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * scale, ChromeFontRole.Body)?.Push())
            TextDrawing.Draw(list, position + new Vector2(52, 33) * scale, ImGui.GetColorU32(GratisTheme.Secondary),
                (result.Distance is { } distance ? $"{distance:0}y · " : "") + result.Place.Name + " · "
                    + result.Place.Subtitle.Split('>').Last().Trim());
        list.PopClipRect();
        ImGui.PopID();
    }

    private void DrawCard(PlaceEntry? place, string title, string tag, Vector4 accent, string subtitle, Vector2 min,
        Vector2 size, bool active, MapDrawingConfig config, float scale) {
        var list = ImGui.GetWindowDrawList();
        var max = min + size;
        if (place != null)
            DrawArt(place, min, size, config, scale);
        else {
            var top = ImGui.GetColorU32(accent with { X = accent.X * .25f, Y = accent.Y * .25f, Z = accent.Z * .25f });
            var bottom = ImGui.GetColorU32(Vector4.Lerp(GratisTheme.Surface with { W = 1 }, accent, .4f));
            list.AddRectFilledMultiColor(min, max, top, top, bottom, bottom);
        }
        list.AddRectFilledMultiColor(min + size * new Vector2(0, .22f), max, 0, 0, 0xf5101012, 0xf5101012);
        list.AddRectFilled(min + new Vector2(12, 12) * scale, min + new Vector2(36, 15) * scale,
            ImGui.GetColorU32(accent), scale);
        using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * scale, ChromeFontRole.Chip)?.Push())
            TextDrawing.Draw(list, min + new Vector2(12, 24) * scale, ImGui.GetColorU32(accent),
                tag);
        list.PushClipRect(min + new Vector2(12 * scale, 0), max - new Vector2(10 * scale, 0), true);
        using (MapDrawContext.Current.GetChromeFont((GratisTheme.TextSize + 4) * scale, ChromeFontRole.Title)?.Push())
            TextDrawing.Draw(list, min + new Vector2(12 * scale, size.Y - 57 * scale),
                ImGui.GetColorU32(GratisTheme.Primary), title);
        using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * scale, ChromeFontRole.Body)?.Push())
            TextDrawing.Draw(list, min + new Vector2(12 * scale, size.Y - 27 * scale),
                ImGui.GetColorU32(GratisTheme.Primary with { W = .7f }), subtitle);
        list.PopClipRect();
        var opacity = UiMotion.Value(ImGui.GetID("##browser-accent"), 7, active ? 1 : 0, initial: 0);
        if (opacity > .001f) {
            var indicator = active ? UiMotion.Selection(selectionMotionKey, min) : min;
            list.AddRect(indicator, indicator + size, ImGui.GetColorU32(accent with { W = opacity }),
                GratisTheme.ButtonRadius * scale, 0, 1.5f * scale);
        }
    }

    private static Vector4 ExpansionAccent(uint id) => id switch {
        0 => GratisTheme.WarmAccent,
        1 => GratisTheme.Crystal,
        2 => new(.85f, .24f, .30f, 1),
        3 => GratisTheme.Link,
        4 => Vector4.Lerp(GratisTheme.Service, GratisTheme.Transport, .5f),
        5 => GratisTheme.Objective,
        _ => GratisTheme.Transport,
    };

    private void DrawArt(PlaceEntry place, Vector2 min, Vector2 size, MapDrawingConfig config, float scale) {
        var list = ImGui.GetWindowDrawList();
        var image = config.PaletteImagery ? source.Image(place) : default;
        if (image.Ready) {
            var sourceAspect = (float)image.Width / image.Height;
            var targetAspect = size.X / size.Y;
            var uvSize = sourceAspect > targetAspect ? new Vector2(targetAspect / sourceAspect, 1)
                : new Vector2(1, sourceAspect / targetAspect);
            var uvMin = (Vector2.One - uvSize) / 2;
            list.AddImage(image.Handle, min, min + size, uvMin, uvMin + uvSize);
        }
        else {
            var accent = ExpansionAccent(place.Expansion);
            var top = ImGui.GetColorU32(accent with { X = accent.X * .25f, Y = accent.Y * .25f, Z = accent.Z * .25f });
            var bottom = ImGui.GetColorU32(Vector4.Lerp(GratisTheme.Surface with { W = 1 }, accent, .4f));
            list.AddRectFilledMultiColor(min, min + size, top, top, bottom, bottom);
            if (config.PaletteImagery) {
                var seed = image.Seed != 0 ? image.Seed : place.MapId + place.Expansion * 31;
                list.PushClipRect(min, min + size, true);
                list.AddCircleFilled(min + size * new Vector2(.75f, .28f), MathF.Min(size.X, size.Y) * .12f,
                    ImGui.GetColorU32(accent with { W = .22f }), 32);
                for (var ridge = 0; ridge < 7; ridge++) {
                    var x = min.X + size.X * (ridge - 1) / 5;
                    var y = min.Y + size.Y * (.28f + ((seed + (uint)ridge * 17) % 31) / 100f);
                    list.AddTriangleFilled(new(x - size.X * .35f, min.Y + size.Y), new(x + size.X * .16f, y),
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