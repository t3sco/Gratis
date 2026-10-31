using System;
using System.Numerics;
using Gratis.Data;
using Gratis.Windows;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class QuickSetupFaces {
    public static readonly ActionProfile Primary = ActionFaces.Action with { Label = ActionLabel.Center, IconSize = 0 };
    public static readonly ActionProfile Quiet = ActionFaces.Quiet with {
        Label = ActionLabel.Center, Ink = GratisTheme.Primary with { W = .85f },
    };
}

public sealed partial class QuickSetup {
    public const string SkipLabel = "Skip, use defaults";
    private const float Pad = 20, Gap = 8, CardHeight = 96, SwatchHeight = 58, Row = 36;
    private bool draw;
    private float fx, fy, fw, fs;
    private int slots;
    private ImDrawListPtr list;

    public void Draw(IMapWindowHost host) {
        if (!Visible)
            return;
        var config = host.Config;
        var s = GratisTheme.ChromeScale;
        var (min, max) = ChromeLayout.RawSafeBounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        var width = MathF.Min(440 * s, max.X - min.X - 2 * Gap * s);
        if (width < 260 * s) {
            Bounds = Vector4.Zero;
            return;
        }
        var shown = Opacity = UiMotion.Visibility(ImGui.GetID("quick-setup"), true);
        using var fade = FrameScopes.PushStyle(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * shown, true);
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push();
        list = ImGui.GetWindowDrawList();
        var pad = Pad * s;
        // Measure first so the surface can be drawn under the content at its final height.
        draw = false;
        fs = s;
        fw = width - 2 * pad;
        fx = fy = 0;
        var height = Body(config) + 2 * pad;
        var pos = new Vector2(MathF.Round((min.X + max.X - width) / 2),
            MathF.Round(MathF.Max(min.Y, (min.Y + max.Y - height) / 2)));
        Bounds = new(pos.X, pos.Y, pos.X + width, pos.Y + height);
        draw = true;
        slots = 0;
        fx = pos.X + pad;
        fy = pos.Y + pad;
        ImGui.PushID("quick-setup");
        try {
            MapWindowDrawing.DrawSurface(list, pos, pos + new Vector2(width, height), GratisTheme.Radius.Card * 1.5f * s);
            Body(config);
        }
        finally {
            ImGui.PopID();
            draw = false;
            slotCount = slots;
        }
    }

    private float Body(SystemConfig config) {
        switch (Step) {
            case QuickSetupStep.Welcome:
                Heading("Welcome to Gratis");
                Para("Gratis is an alternative FFXIV map and a fork of Mappy by MidoriKami, shared under the AGPL "
                    + "with credit to their work. A few quick choices apply to your map as you make them.");
                Footer(config, false, false, "Next", true);
                break;
            case QuickSetupStep.Look:
                Heading("Pick a look");
                Cards(config);
                Para(QuickSetupLooks.All[QuickSetupLooks.Current(config)].Blurb);
                if (!config.IsGratis)
                    Para("Place search, teleport cards and the quest log work in Gratis looks only, so the next step "
                        + "is skipped.", GratisTheme.WarmAccent);
                Para("Marker icons", GratisTheme.Primary, 4);
                Icons(config);
                Footer(config, true, dirty, "Next", true);
                break;
            case QuickSetupStep.Behaviours:
                Heading("Behaviours");
                Para("Each one applies now. Undo puts this page back.");
                for (var i = 0; i < Behaviours.Length; i++)
                    Toggles(config, i);
                Footer(config, true, dirty, "Next", true);
                break;
            default:
                Heading("You're set");
                Para("Open Settings any time with /gratis or the gear on the map toolbar. /gratis map shows or hides "
                    + "the map, /gratis find searches places and /gratis help lists the rest.");
                Para("In Settings, Appearance, Save current settings as profile keeps this setup, and Run Quick setup "
                    + "again replays it.");
                Footer(config, true, false, "Finish", false);
                break;
        }
        return fy;
    }

    private void Heading(string text) {
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * 1.2f * fs, ChromeFontRole.Title)
            ?.Push();
        Text(text, GratisTheme.Primary, 8);
    }

    private void Para(string text, Vector4? colour = null, float gap = Gap) => Text(text, colour ?? GratisTheme.Secondary,
        gap);

    private void Text(string text, Vector4 colour, float gap) {
        var size = TextDrawing.Measure(text, fw);
        if (draw)
            TextDrawing.Draw(list, new(fx, fy), ImGui.GetColorU32(colour), text, fw);
        fy += size.Y + gap * fs;
    }

    private bool Slot(string label, Vector2 min, Vector2 max, bool clicked, bool main = false) {
        var index = slots++;
        if (index >= MaxSlots)
            return clicked;
        hit[index] = new(min.X, min.Y, max.X, max.Y);
        names[index] = label;
        if (main)
            primary = index;
        if (keyboardFocus && Focus() == index)
            list.AddRect(min - new Vector2(2 * fs), max + new Vector2(2 * fs),
                ImGui.GetColorU32(GratisTheme.Crystal), GratisTheme.Radius.Card * fs, ImDrawFlags.None, 1.5f * fs);
        return clicked || pending == index;
    }

    private float ButtonWidth(string label) => MathF.Max(72 * fs, TextDrawing.Measure(label).X + 24 * fs);

    private bool Button(string label, in ActionProfile face, float x, float y, float width, bool main = false) {
        if (!draw)
            return false;
        var pos = new Vector2(x, y);
        var size = new Vector2(width, GratisTheme.ControlHeight * fs);
        ImGui.SetCursorScreenPos(pos);
        ImGui.PushID(slots);
        var clicked = ActionButton.Draw("##button", label, new(face, Size: size));
        ImGui.PopID();
        return Slot(label, pos, pos + size, clicked, main);
    }

    private void Footer(SystemConfig config, bool back, bool undo, string next, bool skip) {
        var gap = Gap * fs;
        var height = GratisTheme.ControlHeight * fs;
        float backWidth = back ? ButtonWidth("Back") : 0, undoWidth = undo ? ButtonWidth("Undo") : 0,
            skipWidth = skip ? ButtonWidth(SkipLabel) : 0, nextWidth = ButtonWidth(next);
        var left = backWidth + undoWidth + (back && undo ? gap : 0);
        var right = skipWidth + nextWidth + (skip ? gap : 0);
        var oneRow = left + right + (left > 0 ? gap : 0) <= fw;
        var rightRow = fy + 4 * fs;
        var leftRow = oneRow ? rightRow : rightRow + height + gap;
        var x = fx;
        if (back) {
            if (Button("Back", QuickSetupFaces.Quiet, x, leftRow, backWidth))
                Back(config);
            x += backWidth + gap;
        }
        if (undo && Button("Undo", QuickSetupFaces.Quiet, x, leftRow, undoWidth))
            Undo(config);
        x = fx + fw - right;
        if (skip) {
            if (Button(SkipLabel, QuickSetupFaces.Quiet, x, rightRow, skipWidth))
                Skip(config);
            x += skipWidth + gap;
        }
        if (Button(next, QuickSetupFaces.Primary, x, rightRow, nextWidth, true)) {
            if (Step == QuickSetupStep.Done)
                Finish(config);
            else
                Next(config);
        }
        fy = (oneRow ? rightRow : leftRow) + height;
    }

    private void Cards(SystemConfig config) {
        var looks = QuickSetupLooks.All;
        var gap = Gap * fs;
        var width = (fw - gap * (looks.Count - 1)) / looks.Count;
        var height = CardHeight * fs;
        var current = QuickSetupLooks.Current(config);
        for (var i = 0; draw && i < looks.Count; i++) {
            var min = new Vector2(fx + i * (width + gap), fy);
            var max = min + new Vector2(width, height);
            ImGui.SetCursorScreenPos(min);
            ImGui.PushID(slots);
            var clicked = ChromeControls.Button("##card", new Vector2(width, height));
            var hovered = ImGui.IsItemHovered();
            ImGui.PopID();
            var radius = GratisTheme.Radius.Card * fs;
            list.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1, 1, 1, hovered ? .07f : .035f)), radius);
            list.AddRect(min, max, ImGui.GetColorU32(i == current ? GratisTheme.WarmAccent : GratisTheme.Border),
                radius, ImDrawFlags.None, (i == current ? 1.5f : 1) * fs);
            Swatch(min + new Vector2(6 * fs), new Vector2(max.X - 6 * fs, min.Y + (6 + SwatchHeight) * fs), looks[i],
                config);
            var name = looks[i].Name;
            var size = TextDrawing.Measure(name);
            TextDrawing.Draw(list, new(min.X + (width - size.X) / 2, max.Y - size.Y - 8 * fs),
                ImGui.GetColorU32(i == current ? GratisTheme.Primary : GratisTheme.Secondary), name);
            if (Slot(name, min, max, clicked))
                ChooseLook(config, i);
        }
        fy += height + Gap * fs;
    }

    /// <summary>A miniature of the look: corner radius, frame, panel material and accent, drawn live from its numbers.</summary>
    private void Swatch(Vector2 min, Vector2 max, QuickSetupLook look, SystemConfig config) {
        var round = MathF.Min(look.Radius * fs * .4f, MathF.Min(max.X - min.X, max.Y - min.Y) / 2);
        var accent = look.Classic ? new Vector4(1, 1, 1, .55f) : config.AccentColor with { W = .85f };
        list.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(.13f, .17f, .2f, 1)), round);
        list.AddCircleFilled(min + (max - min) * new Vector2(.28f, .66f), 11 * fs,
            ImGui.GetColorU32(new Vector4(.28f, .36f, .3f, .8f)));
        list.AddCircleFilled(min + (max - min) * new Vector2(.72f, .6f), 7 * fs,
            ImGui.GetColorU32(new Vector4(.3f, .34f, .42f, .8f)));
        list.AddCircleFilled(min + (max - min) * new Vector2(.58f, .4f), 3 * fs, ImGui.GetColorU32(accent));
        var pill = new Vector2((max.X - min.X) * .46f, 9 * fs);
        var top = min + new Vector2((max.X - min.X - pill.X) / 2, 7 * fs);
        if (look.Classic)
            for (var i = 0; i < 3; i++)
                list.AddRectFilled(min + new Vector2((6 + i * 12) * fs, 6 * fs),
                    min + new Vector2((14 + i * 12) * fs, 14 * fs), ImGui.GetColorU32(new Vector4(1, 1, 1, .5f)));
        else {
            list.AddRectFilled(top, top + pill, ImGui.GetColorU32(new Vector4(1, 1, 1, look.Glass ? .2f : .12f)),
                GratisTheme.Radius.Pill(pill));
            if (look.Glass)
                list.AddRect(top, top + pill, ImGui.GetColorU32(new Vector4(1, 1, 1, .3f)),
                    GratisTheme.Radius.Pill(pill), ImDrawFlags.None, fs);
        }
        list.AddRect(min, max, ImGui.GetColorU32(accent), round, ImDrawFlags.None, look.Classic ? 1.5f * fs : fs);
    }

    private void Icons(SystemConfig config) {
        var width = (fw - Gap * fs) / 2;
        var height = (GratisTheme.ControlHeight - 4) * fs;
        for (var i = 0; draw && i < 2; i++) {
            var game = i == 0;
            var style = game ? MarkerIconStyle.Game : MarkerIconStyle.Icons;
            var label = game ? "Game icons" : "Gratis icons";
            var min = new Vector2(fx + i * (width + Gap * fs), fy);
            ImGui.SetCursorScreenPos(min);
            ImGui.PushID(slots);
            var clicked = SegmentedChip.Draw("##icons", label, config.Markers.IconStyle == style,
                new(ChipFaces.Regular, GratisTheme.WarmAccent, new Vector2(width, height), fs, Centered: true));
            ImGui.PopID();
            if (Slot(label, min, min + new Vector2(width, height), clicked))
                ChooseIcons(config, style);
        }
        fy += (GratisTheme.ControlHeight - 4) * fs + Gap * fs;
    }

    private void Toggles(SystemConfig config, int index) {
        var item = Behaviours[index];
        var toggle = new Vector2(36, GratisTheme.ControlHeight) * SettingsTheme.Scale;
        var textWidth = fw - toggle.X - 12 * fs;
        var label = TextDrawing.Measure(item.Label, textWidth);
        var detail = TextDrawing.Measure(item.Detail, textWidth);
        var height = MathF.Max(toggle.Y, label.Y + 2 * fs + detail.Y);
        if (draw) {
            TextDrawing.Draw(list, new(fx, fy), ImGui.GetColorU32(GratisTheme.Primary), item.Label, textWidth);
            TextDrawing.Draw(list, new(fx, fy + label.Y + 2 * fs), ImGui.GetColorU32(GratisTheme.Secondary),
                item.Detail, textWidth);
            var value = BehaviourValue(config, index);
            var shown = value;
            ImGui.SetCursorScreenPos(new(fx + fw - toggle.X, fy));
            ImGui.PushID(slots);
            ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
            var clicked = SettingsTheme.Toggle("##toggle", ref shown);
            ImGui.PopStyleColor(3);
            ImGui.PopID();
            var min = ImGui.GetItemRectMin();
            if (Slot(item.Label, min, ImGui.GetItemRectMax(), clicked))
                Toggle(config, index, !value);
        }
        fy += height + Gap * fs;
    }
}
