using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class QuestLogPanel {
    private QuestLogQuest? detailQuest;
    private float detailOpacity;
    private bool detailKeyboard;
    public bool DetailOpen { get; private set; }
    public uint DetailQuestId => DetailOpen ? detailQuest?.RowId ?? 0 : 0;
    public Vector4 DetailBounds => !suspended && (DetailOpen || detailOpacity > .001f) ? Layout.Detail : Vector4.Zero;
    public Vector4 DetailMapBounds { get; private set; }
    public Vector4 DetailCloseBounds { get; private set; }

    private void ShowSearch() {
        if (Layout.DetailOverlay)
            CloseDetail();
        searchOpen = focusSearch = true;
    }

    private void HideSearch() {
        searchOpen = focusSearch = false;
        query = "";
    }

    private void CloseDetail() {
        DetailOpen = false;
        itemTooltip.Reset();
    }

    private void ShowOnMap(QuestLogQuest quest) {
        if (source?.Show(quest) != true)
            return;
        CloseDetail();
        if (!MapDrawContext.Current.Config.QuestLogPinned) {
            // The map the log opened is the one the quest is being shown on, so closing the log must not close it.
            KeepMapOnClose = true;
            CloseLog();
            KeepMapOnClose = false;
        }
    }

    public static DrawTexture RowIcon(QuestLogQuest quest) {
        var context = MapDrawContext.Current;
        if (quest.Bucket == Controllers.QuestBucket.Scenario && quest.ExpansionIcon != 0) {
            var crest = context.GetExpansionCrest(quest.ExpansionIcon);
            if (crest.Handle != default)
                return crest;
        }
        return context.GetIcon(quest.Icon);
    }

    private void DrawDetail(IMapWindowHost host, float s, ImDrawListPtr parent) {
        detailKeyboard = false;
        DetailMapBounds = DetailCloseBounds = Vector4.Zero;
        detailOpacity = detailQuest == null ? 0
            : UiMotion.Visibility(ImGui.GetID("##quest-detail-visibility-" + motionEpoch), IsOpen && DetailOpen);
        if (detailQuest == null || detailOpacity <= .001f)
            return;
        var bounds = Layout.Detail;
        var origin = new Vector2(bounds.X, bounds.Y);
        var size = new Vector2(bounds.Z - bounds.X, bounds.W - bounds.Y);
        if (size.Y < 40 * s)
            return;
        var slide = UiMotion.Full ? new Vector2((1 - detailOpacity) * 18 * s, 0) : Vector2.Zero;
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push();
        using (var surface = new UiMotion.VisualScope(parent, origin, detailOpacity, slide))
            MapWindowDrawing.DrawSurface(parent, origin, origin + size, GratisTheme.Radius.Popup * s, surfaceOpacity: 1);
        ImGui.SetCursorScreenPos(origin);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Primary);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, GratisTheme.Secondary);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(16) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 6) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(12, 8) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, GratisTheme.Radius.Control * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 4 * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, GratisTheme.Radius.Pill(new Vector2(4 * s)));
        var visible = BeginDetail(size, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse | (!DetailOpen ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None));
        try {
            if (!visible)
                return;
            using var motion = new UiMotion.VisualScope(ImGui.GetWindowDrawList(), origin, detailOpacity, slide);
            ImGui.BeginDisabled(!DetailOpen);
            try {
                DrawDetailContent(detailQuest, s);
                detailKeyboard = ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows);
                if (Layout.DetailOverlay) {
                    keyboard = rowKeyboard || detailKeyboard;
                    if (DetailOpen && !searchOpen && keyboard && (ImGui.IsKeyPressed(ImGuiKey.M, false)
                        || ImGui.GetIO().KeyShift && ImGui.IsKeyPressed(ImGuiKey.Enter, false)))
                        ShowOnMap(detailQuest);
                }
                if (PointerOver) MapDrawContext.Current.Chrome.Reveal(host.Now);
            }
            finally { ImGui.EndDisabled(); }
        }
        finally { ImGui.EndChild(); ImGui.PopStyleVar(6); ImGui.PopStyleColor(3); }
    }

    private static bool BeginDetail(Vector2 size, ImGuiWindowFlags flags) =>
#if GRATIS_PREVIEW
        ImGui.BeginChild("##quest-detail", size, ImGuiChildFlags.AlwaysUseWindowPadding, flags);
#else
        ImGui.BeginChild("##quest-detail", size, false, flags | ImGuiWindowFlags.AlwaysUseWindowPadding);
#endif
}
