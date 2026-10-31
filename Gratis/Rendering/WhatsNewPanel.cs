using System;
using System.Numerics;
using Gratis.Windows;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class WhatsNewFaces {
    public static readonly ActionProfile Primary = ActionFaces.Action with { Label = ActionLabel.Center, IconSize = 0 };
    public static readonly ActionProfile Quiet = ActionFaces.Quiet with { Label = ActionLabel.Center };
}

public sealed class WhatsNewPanel {
    public const string GotItLabel = "Got it", SeeAllLabel = "See all updates";
    public const float Width = 560, Height = 460;
    private const float Pad = 20, Rounding = 12, CloseSize = 28, ChipHeight = 22, ButtonGap = 8;
    public const ImGuiWindowFlags Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoSavedSettings;
    private static readonly string[] KindLabels = [
        Changelog.Label(ChangeKind.New).ToUpperInvariant(), Changelog.Label(ChangeKind.Improved).ToUpperInvariant(),
        Changelog.Label(ChangeKind.Fixed).ToUpperInvariant()];
    private int opens;
    private ChangelogRelease? chipFor;
    private string chipText = "";

    public Action? Save { get; set; }
    public Action? OpenChangelog { get; set; }
    public Vector4 GotItBounds { get; private set; }
    public Vector4 SeeAllBounds { get; private set; }
    public Vector4 CloseBounds { get; private set; }
    public Vector4 BodyBounds { get; private set; }
    public int GroupsDrawn { get; private set; }
    public int BulletsDrawn { get; private set; }
    public bool Scrolls { get; private set; }
    public float ScrollY { get; private set; }

    public void Open() => opens++;

    public static Vector2 Size(float scale) => new Vector2(Width, Height) * scale;

    public static Vector2 Centred(Vector2 viewportPos, Vector2 viewportSize, float scale) =>
        viewportPos + new Vector2(MathF.Round((viewportSize.X - Width * scale) / 2),
            MathF.Round((viewportSize.Y - Height * scale) / 2));

    public static void PushStyle() {
        var s = MapDrawContext.Current.GlobalScale;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Pad * s));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, Rounding * s);
    }

    public static void PopStyle() => ImGui.PopStyleVar(3);

    // Draws inside an open window and returns false once the player has dismissed it.
    public bool Draw(MapDrawingConfig config) {
        var s = MapDrawContext.Current.GlobalScale;
        var release = Changelog.Releases[0];
        GroupsDrawn = BulletsDrawn = 0;
        ImGui.PushID(opens);
        var fade = UiMotion.Visibility(ImGui.GetID("whats-new"), true);
        ImGui.PopID();
        using var alpha = FrameScopes.PushStyle(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * fade, true);
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        ImGui.PushClipRect(min - new Vector2(4 * s), max + new Vector2(4 * s), false);
        MapWindowDrawing.DrawSurface(ImGui.GetWindowDrawList(), min, max, Rounding * s);
        ImGui.PopClipRect();
        var closed = false;
        var seeAll = false;
        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows)) {
            ImGui.SetNextFrameWantCaptureKeyboard(true);
            closed = ImGui.IsKeyPressed(ImGuiKey.Escape, false) || ImGui.IsKeyPressed(ImGuiKey.GamepadFaceRight, false);
        }
        Header(release, s, ref closed);
        Body(release, s);
        Footer(s, ref closed, ref seeAll);
        if (!closed && !seeAll)
            return true;
        WhatsNew.Dismiss(config);
        Save?.Invoke();
        if (seeAll)
            OpenChangelog?.Invoke();
        return false;
    }

    private void Header(ChangelogRelease release, float s, ref bool closed) {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        using (SettingsTheme.Font(24, ChromeFontRole.Title))
            ImGui.TextUnformatted("Gratis");
        using (SettingsTheme.Font(14)) {
            ImGui.TextColored(GratisTheme.Secondary, "What's new");
        }
        var after = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new(origin.X + width - CloseSize * s, origin.Y));
        if (ChromeControls.IconButton("##whats-new-close", GratisIcons.X, "Close", new(CloseSize * s), s))
            closed = true;
        CloseBounds = Bounds();
        ImGui.SetCursorScreenPos(after);
        ImGui.Dummy(new(0, 6 * s));
        if (!ReferenceEquals(chipFor, release)) {
            chipFor = release;
            chipText = "v" + release.Version;
        }
        using (SettingsTheme.Font(12)) {
            var chip = ImGui.CalcTextSize(chipText);
            var size = new Vector2(chip.X + 20 * s, ChipHeight * s);
            var pos = ImGui.GetCursorScreenPos();
            var list = ImGui.GetWindowDrawList();
            var radius = GratisTheme.Radius.Pill(size);
            list.AddRectFilled(pos, pos + size, ImGui.GetColorU32(GratisTheme.Crystal with { W = .14f }), radius);
            list.AddRect(pos, pos + size, ImGui.GetColorU32(GratisTheme.Crystal with { W = .4f }), radius);
            list.AddText(pos + new Vector2(10 * s, (size.Y - chip.Y) / 2), ImGui.GetColorU32(GratisTheme.Crystal),
                chipText);
            var date = ImGui.CalcTextSize(release.Date);
            list.AddText(pos + new Vector2(size.X + 10 * s, (size.Y - date.Y) / 2),
                ImGui.GetColorU32(GratisTheme.Secondary), release.Date);
            ImGui.Dummy(new(size.X + 10 * s + date.X, size.Y));
        }
        ImGui.Dummy(new(0, 2 * s));
        using (SettingsTheme.Font(16, ChromeFontRole.Title)) {
            ImGui.PushTextWrapPos(0);
            ImGui.TextColored(GratisTheme.WarmAccent, release.Headline);
            ImGui.PopTextWrapPos();
        }
        ImGui.Dummy(new(0, 2 * s));
        var line = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddLine(line, line + new Vector2(width, 0), ImGui.GetColorU32(GratisTheme.Border));
        ImGui.Dummy(new(0, 1));
    }

    private void Body(ChangelogRelease release, float s) {
        var footer = (GratisTheme.ControlHeight + ButtonGap) * s;
        var avail = ImGui.GetContentRegionAvail();
        UiMotion.Child("##whats-new-body", new(0, MathF.Max(1, avail.Y - footer)), ImGuiWindowFlags.NoScrollWithMouse);
        UiMotion.Scroll();
        Scrolls = ImGui.GetScrollMaxY() > 0;
        ScrollY = ImGui.GetScrollY();
        BodyBounds = new(ImGui.GetWindowPos().X, ImGui.GetWindowPos().Y, ImGui.GetWindowPos().X + ImGui.GetWindowWidth(),
            ImGui.GetWindowPos().Y + ImGui.GetWindowHeight());
        var list = ImGui.GetWindowDrawList();
        var indent = 16 * s;
        ImGui.Dummy(new(0, 2 * s));
        for (var g = 0; g < release.Groups.Count; g++) {
            var group = release.Groups[g];
            var tint = group.Kind switch {
                ChangeKind.New => GratisTheme.Crystal,
                ChangeKind.Improved => GratisTheme.WarmAccent,
                _ => GratisTheme.Secondary,
            };
            if (GroupsDrawn++ > 0)
                ImGui.Dummy(new(0, 4 * s));
            using (SettingsTheme.Font(12))
                ImGui.TextColored(tint, KindLabels[(int)group.Kind]);
            for (var i = 0; i < group.Items.Count; i++) {
                BulletsDrawn++;
                var pos = ImGui.GetCursorScreenPos();
                list.AddCircleFilled(pos + new Vector2(5 * s, ImGui.GetTextLineHeight() / 2), 2 * s,
                    ImGui.GetColorU32(tint));
                ImGui.Indent(indent);
                ImGui.PushTextWrapPos(0);
                ImGui.TextUnformatted(group.Items[i]);
                ImGui.PopTextWrapPos();
                ImGui.Unindent(indent);
            }
        }
        ImGui.EndChild();
    }

    private void Footer(float s, ref bool closed, ref bool seeAll) {
        var origin = ImGui.GetCursorScreenPos();
        var right = origin.X + ImGui.GetContentRegionAvail().X;
        var height = GratisTheme.ControlHeight * s;
        ImGui.GetWindowDrawList().AddLine(new(origin.X, origin.Y - 4 * s), new(right, origin.Y - 4 * s),
            ImGui.GetColorU32(GratisTheme.Border));
        var gotWidth = MathF.Max(96 * s, TextDrawing.Measure(GotItLabel).X + 28 * s);
        var allWidth = TextDrawing.Measure(SeeAllLabel).X + 28 * s;
        ImGui.SetCursorScreenPos(origin);
        if (ActionButton.Draw("##see-all", SeeAllLabel, new(WhatsNewFaces.Quiet, Size: new(allWidth, height), Scale: s)))
            seeAll = true;
        SeeAllBounds = Bounds();
        ImGui.SetCursorScreenPos(new(right - gotWidth, origin.Y));
        if (ActionButton.Draw("##got-it", GotItLabel, new(WhatsNewFaces.Primary, Size: new(gotWidth, height), Scale: s)))
            closed = true;
        GotItBounds = Bounds();
    }

    private static Vector4 Bounds() {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        return new(min.X, min.Y, max.X, max.Y);
    }
}
