using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed class ItemTooltipDrawing {
    private Vector2 menuAnchor;
    private bool menuOpen;
    private uint menuItem;
    private readonly ItemTooltipHover hover = new();
    private readonly ItemTooltipCache cache = new();
    private QuestLogRewardItem? shownReward;
    private uint summary;
    private bool hoveredThisFrame;
    private readonly MenuRowCache linkRow = new(ItemTooltipModel.LinkEntry), copyRow = new(ItemTooltipModel.CopyNameEntry),
        searchRow = new(ItemTooltipModel.SearchEntry), tryOnRow = new(ItemTooltipModel.TryOnEntry);
    public Vector4 MenuBounds { get; private set; }
    public static Vector4 LastTooltipBounds { get; private set; }
    public static uint LastTooltipItem { get; private set; }
    public static int LastTooltipFrame { get; private set; } = -1;

    public void Prepare(uint quest, QuestLogRewards? rewards, IItemTooltipSource? source) {
        hoveredThisFrame = false;
        if (summary != quest) {
            Reset();
            summary = quest;
        }
        cache.Prepare(rewards?.Items, source);
    }

    public void Reset() {
        hover.Reset();
        shownReward = null;
        summary = menuItem = 0;
        menuOpen = false;
        MenuBounds = Vector4.Zero;
        LastTooltipBounds = Vector4.Zero;
        LastTooltipItem = 0;
        LastTooltipFrame = -1;
    }

    public void Draw(QuestLogRewardItem reward, IItemTooltipSource? source, float s) {
        var hovered = ImGui.IsItemHovered();
        const string popupId = "##reward-context";
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
            menuAnchor = ImGui.GetMousePos();
            menuOpen = true;
            menuItem = reward.Id;
            ImGui.OpenPopup(popupId);
        }
        if (menuItem == reward.Id) DrawMenu(popupId, reward, source, s);
        if (hovered && !menuOpen) {
            hoveredThisFrame = true;
            if (hover.Observe(reward.Id, ImGui.GetTime())) shownReward = reward;
        }
    }

    public void Finish(IItemTooltipSource? source, float s) {
        hover.EndFrame(hoveredThisFrame, ImGui.GetTime());
        if (menuOpen) {
            hover.Reset();
            shownReward = null;
        }
        if (shownReward is { } reward && hover.Visible(ImGui.GetTime())) {
            var item = cache.Get(reward.Id) ?? new ItemTooltipRecord(reward.Id, reward.Name, reward.Icon);
            DrawTooltip(item, reward.Count, source?.OwnedCount(reward.Id), s);
        }
    }

    private void DrawMenu(string id, QuestLogRewardItem reward, IItemTooltipSource? source, float s) {
        using var style = PopupSurface.Push(PopupFaces.Chrome, scale: s);
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push();
        var entries = ItemTooltipModel.MenuEntries(source?.CanLink == true, source?.CanSearch == true,
            source?.CanTryOn(reward.Id) == true);
        var size = new Vector2(208, 24 + entries.Count * 36 + (entries.Count - 1) * 4) * s;
        var viewport = ImGui.GetWindowViewport();
        var min = Vector2.Clamp(menuAnchor, viewport.Pos,
            Vector2.Max(viewport.Pos, viewport.Pos + viewport.Size - size));
        var escape = ImGui.IsKeyPressed(ImGuiKey.Escape, false);
        using var popup = UiMotion.Popup(id, menuOpen && !escape, min, size, scale: s);
        menuOpen = popup.Interactive;
        MenuBounds = popup.Visible ? new(min.X, min.Y, min.X + size.X, min.Y + size.Y) : Vector4.Zero;
        if (!popup.Visible) return;
        ImGui.BeginDisabled(!popup.Interactive);
        try {
            foreach (var entry in entries) {
                var cached = entry == ItemTooltipModel.TryOnEntry ? tryOnRow : entry == ItemTooltipModel.LinkEntry ? linkRow
                    : entry == ItemTooltipModel.SearchEntry ? searchRow : copyRow;
                var enabled = entry != ItemTooltipModel.TryOnEntry || source?.TryOnEnabled == true;
                ImGui.BeginDisabled(!enabled);
                var row = new MenuRow(cached, new(RowFaces.Reward, s, Width: size.X - 24 * s, Enabled: enabled));
                row.Draw(entry);
                ImGui.EndDisabled();
                if (!row.Clicked) continue;
                if (entry == ItemTooltipModel.TryOnEntry) source?.TryOn(reward.Id);
                else if (entry == ItemTooltipModel.LinkEntry) source?.Link(reward.Id);
                else if (entry == ItemTooltipModel.SearchEntry) source?.Search(reward.Id);
                else ImGui.SetClipboardText(reward.Name);
                ImGui.CloseCurrentPopup();
                menuOpen = false;
            }
        }
        finally { ImGui.EndDisabled(); }
        if (popup.Interactive || escape) {
            ImGui.SetNextFrameWantCaptureKeyboard(true);
            ImGui.SetNextFrameWantCaptureMouse(true);
        }
    }

    public static void DrawTooltip(ItemTooltipRecord item, uint count, int? owned, float s) {
#if GRATIS_PREVIEW
        using var benchmark = BenchmarkProbe.Measure(BenchmarkSection.ItemTooltip);
#endif
        var context = MapDrawContext.Current;
        using var font = context.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push();
        var width = MathF.Min(336 * s, ImGui.GetWindowViewport().Size.X - 40 * s);
        var presentation = ItemTooltipModel.For(item);
        var sections = presentation.Sections;
        var stack = presentation.Stack(item, count, owned);
        var title = presentation.Title;
        var titleHeight = 0f;
        using (context.GetChromeFont(16 * s, ChromeFontRole.Title)?.Push())
            titleHeight = ImGui.CalcTextSize(title, false, width - 60 * s).Y;
        var headerHeight = MathF.Max(48 * s, titleHeight + (item.Category.Length > 0 ? 22 * s : 0));
        var height = 24 * s + headerHeight + (stack.Length > 0 ? 24 * s : 0);
        foreach (var section in sections)
            height += 40 * s + (section.Text.Length > 0 ? ImGui.CalcTextSize(section.Text, false, width).Y : 0)
                + (section.Tags.Count > 0 ? 28 * s : 0);
        UiMotion.TooltipContent(() => {
            var p = ImGui.GetCursorScreenPos();
            var list = ImGui.GetWindowDrawList();
            var icon = context.GetIcon(item.Icon);
            if (icon.Handle != default) list.AddImage(icon.Handle, p, p + new Vector2(44 * s));
            ImGui.SetCursorScreenPos(p + new Vector2(60 * s, 0));
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width - 60 * s);
            using (context.GetChromeFont(16 * s, ChromeFontRole.Title)?.Push())
                ImGui.TextColored(Rarity(item.Rarity), title);
            if (item.Category.Length > 0) {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 60 * s);
                ImGui.TextColored(GratisTheme.Secondary, item.Category);
            }
            ImGui.PopTextWrapPos();
            ImGui.SetCursorScreenPos(p + new Vector2(0, headerHeight));
            if (stack.Length > 0) ImGui.TextColored(GratisTheme.Secondary, stack);
            foreach (var section in sections) DrawSection(section, presentation.Heading(section.Heading), width, s);
            var pos = ImGui.GetWindowPos();
            var size = ImGui.GetWindowSize();
            LastTooltipBounds = new(pos.X, pos.Y, pos.X + size.X, pos.Y + size.Y);
            LastTooltipItem = item.Id;
            LastTooltipFrame = ImGui.GetFrameCount();
        }, new(width + 24 * s, height), s);
    }

    private static void DrawSection(ItemTooltipSection section, string heading, float width, float s) {
        SectionHeader.Draw(heading, SectionFaces.ItemTooltip, s, width: width);
        if (section.Text.Length > 0) {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
            ImGui.TextUnformatted(section.Text);
            ImGui.PopTextWrapPos();
        }
        if (section.Tags.Count == 0) return;
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos() + new Vector2(0, 2 * s);
        var x = 0f;
        var height = 0f;
        using (MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s, ChromeFontRole.Chip)?.Push())
            foreach (var tag in section.Tags) {
                var size = Pill.Measure(tag, PillFaces.PaletteRow, s);
                PaletteRowDrawing.Pill(list, origin + new Vector2(x, 0), tag, GratisTheme.Secondary, s);
                x += size.X + 6 * s;
                height = size.Y;
            }
        ImGui.Dummy(new(width, height + 2 * s));
    }

    private static Vector4 Rarity(byte rarity) => rarity switch {
        2 => new(.58f, .85f, .6f, 1), 3 => GratisTheme.Crystal,
        4 => new(.86f, .65f, 1, 1), 7 => GratisTheme.WarmAccent, _ => GratisTheme.Primary,
    };
}
