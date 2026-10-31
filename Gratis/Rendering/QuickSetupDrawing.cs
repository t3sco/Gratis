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
    public const string SkipLabel = "Skip, use defaults", CompactSkipLabel = "Skip";
    private const float Pad = 20, CompactPad = 12, Gap = 8, CardHeight = 96, SwatchHeight = 58, Row = 36,
        WheelStep = 48, FooterTop = 4, MinBody = 24, MinWidth = 150;
    private bool draw, fBack, fUndo, fSkip, compactFooter, inFooter;
    private float fx, fy, fw, fs, viewTop, viewBottom;
    private int slots;
    private string fNext = "";
    private ImDrawListPtr list;
    // Footer buttons in slot order (Back, Undo, Skip, Next): position relative to the footer block, and width.
    private readonly Vector2[] footerAt = new Vector2[4];
    private readonly float[] footerWidth = new float[4];
    /// <summary>The label the Skip button carries: shortened when the panel is too narrow for the full one.</summary>
    public string SkipText => compactFooter ? CompactSkipLabel : SkipLabel;
    /// <summary>True when the body is taller than the room and scrolls above a footer that stays put.</summary>
    public bool Scrolls { get; private set; }

    public void Draw(IMapWindowHost host) {
        if (!Visible)
            return;
        var config = host.Config;
        var s = GratisTheme.ChromeScale;
        var (min, max) = ChromeLayout.RawSafeBounds(ImGui.GetWindowPos(), ImGui.GetWindowSize());
        var width = MathF.Min(440 * s, max.X - min.X - 2 * Gap * s);
        var room = max.Y - min.Y;
        var pad = Pad * s;
        // Text is measured in the body font, so it is pushed before anything is laid out.
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push();
        var (body, footer) = Measure(config, s, width, pad);
        if (body + footer + 2 * pad > room) {
            pad = MathF.Min(pad, CompactPad * s);
            (body, footer) = Measure(config, s, width, pad);
        }
        var height = MathF.Min(body + footer + 2 * pad, room);
        var view = height - 2 * pad - footer;
        if (width < MinWidth * s || view < MinBody * s) {
            Visible = InputActive = fits = Scrolls = false;
            Bounds = Vector4.Zero;
            slotCount = 0;
            return;
        }
        fits = true;
        Scrolls = body > view;
        scroll = Scrolls ? Math.Clamp(scroll, 0, body - view) : 0;
        var shown = Opacity = UiMotion.Visibility(ImGui.GetID("quick-setup"), true);
        using var fade = FrameScopes.PushStyle(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * shown, true);
        list = ImGui.GetWindowDrawList();
        var pos = new Vector2(MathF.Round((min.X + max.X - width) / 2),
            MathF.Round(MathF.Max(min.Y, (min.Y + max.Y - height) / 2)));
        Bounds = new(pos.X, pos.Y, pos.X + width, pos.Y + height);
        draw = true;
        slots = 0;
        fx = pos.X + pad;
        viewTop = pos.Y + pad;
        viewBottom = viewTop + (Scrolls ? view : body);
        fy = viewTop - scroll;
        ImGui.PushID("quick-setup");
        try {
            MapWindowDrawing.DrawSurface(list, pos, pos + new Vector2(width, height), GratisTheme.Radius.Card * 1.5f * s);
            // The body scrolls inside its own clip; the footer sits below it and is never clipped.
            ImGui.PushClipRect(new(pos.X, viewTop), new(pos.X + width, viewBottom), true);
            try {
                Body(config);
            }
            finally {
                ImGui.PopClipRect();
            }
            if (Scrolls)
                ScrollBar(pos.X + width - 6 * s, body, view, s);
            inFooter = true;
            DrawFooter(config, pos.Y + height - pad - footer);
        }
        finally {
            ImGui.PopID();
            draw = inFooter = false;
            slotCount = slots;
        }
    }

    /// <summary>Lays the step out without drawing: the body height, and the height the footer needs.</summary>
    private (float Body, float Footer) Measure(SystemConfig config, float s, float width, float pad) {
        draw = false;
        fs = s;
        fw = width - 2 * pad;
        fx = fy = 0;
        Body(config);
        return (fy, FooterLayout());
    }

    private void ScrollBar(float x, float body, float view, float s) {
        var thumb = MathF.Max(16 * s, view * view / body);
        var top = viewTop + (view - thumb) * (body > view ? scroll / (body - view) : 0);
        list.AddRectFilled(new(x, top), new(x + 2 * s, top + thumb), ImGui.GetColorU32(GratisTheme.Border), s);
    }

    private void Body(SystemConfig config) {
        switch (Step) {
            case QuickSetupStep.Welcome:
                Heading("Welcome to Gratis");
                Para("Gratis is an alternative FFXIV map and a fork of Mappy by MidoriKami, shared under the AGPL "
                    + "with credit to their work. A few quick choices apply to your map as you make them.");
                Footer(false, false, "Next", true);
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
                Footer(true, HasChange, "Next", true);
                break;
            case QuickSetupStep.Behaviours:
                Heading("Behaviours");
                Para("Each one applies now. Undo puts this page back.");
                for (var i = 0; i < Behaviours.Length; i++)
                    Toggles(config, i);
                Footer(true, HasChange, "Next", true);
                break;
            default:
                Heading("You're set");
                Para("Open Settings any time with /gratis or the gear on the map toolbar. /gratis map shows or hides "
                    + "the map, /gratis find searches places and /gratis help lists the rest.");
                Para("In Settings, Appearance, Save current settings as profile keeps this setup, and Run Quick setup "
                    + "again replays it.");
                Footer(true, false, "Finish", false);
                break;
        }
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
        if (keyboardFocus && Focus() == index) {
            list.AddRect(min - new Vector2(2 * fs), max + new Vector2(2 * fs),
                ImGui.GetColorU32(GratisTheme.Crystal), GratisTheme.Radius.Card * fs, ImDrawFlags.None, 1.5f * fs);
            // Scroll the body, from the next frame, until the focused control is fully in view.
            if (reveal && inFooter)
                reveal = false;
            else if (reveal && min.Y < viewTop)
                scroll -= viewTop - min.Y + 4 * fs;
            else if (reveal && max.Y > viewBottom)
                scroll += max.Y - viewBottom + 4 * fs;
            else
                reveal = false;
        }
        return clicked || pending == index;
    }

    private float ButtonWidth(string label) => compactFooter
        ? MathF.Min(fw, MathF.Max(52 * fs, TextDrawing.Measure(label).X + 16 * fs))
        : MathF.Max(72 * fs, TextDrawing.Measure(label).X + 24 * fs);

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

    private void Footer(bool back, bool undo, string next, bool skip) {
        (fBack, fUndo, fNext, fSkip) = (back, undo, next, skip);
    }

    /// <summary>
    /// Places the footer buttons and returns the block's height. Normally Back and Undo sit left, Skip and Next right,
    /// on one row or two. If even that is too wide the labels shorten and the buttons flow in rows.
    /// </summary>
    private float FooterLayout() {
        var gap = Gap * fs;
        var height = GratisTheme.ControlHeight * fs;
        var top = FooterTop * fs;
        compactFooter = false;
        var rows = Place(gap, height, top);
        if (rows == 0) {
            compactFooter = true;
            rows = PlaceFlow(gap, height, top);
        }
        return top + rows * height + (rows - 1) * gap;
    }

    // Returns the number of rows used, or 0 when a group is wider than the panel.
    private int Place(float gap, float height, float top) {
        float backWidth = fBack ? ButtonWidth("Back") : 0, undoWidth = fUndo ? ButtonWidth("Undo") : 0,
            skipWidth = fSkip ? ButtonWidth(SkipLabel) : 0, nextWidth = ButtonWidth(fNext);
        var left = backWidth + undoWidth + (fBack && fUndo ? gap : 0);
        var right = skipWidth + nextWidth + (fSkip ? gap : 0);
        var oneRow = left + right + (left > 0 ? gap : 0) <= fw;
        if (!oneRow && (left > fw || right > fw))
            return 0;
        var rightRow = top;
        var leftRow = oneRow ? rightRow : rightRow + height + gap;
        footerWidth[0] = backWidth;
        footerWidth[1] = undoWidth;
        footerWidth[2] = skipWidth;
        footerWidth[3] = nextWidth;
        footerAt[0] = new(0, leftRow);
        footerAt[1] = new(backWidth + (fBack ? gap : 0), leftRow);
        footerAt[2] = new(fw - right, rightRow);
        footerAt[3] = new(fw - right + skipWidth + (fSkip ? gap : 0), rightRow);
        return oneRow ? 1 : 2;
    }

    private int PlaceFlow(float gap, float height, float top) {
        var x = 0f;
        var row = 0;
        for (var i = 0; i < 4; i++) {
            var label = i switch { 0 => "Back", 1 => "Undo", 2 => CompactSkipLabel, _ => fNext };
            var shown = i switch { 0 => fBack, 1 => fUndo, 2 => fSkip, _ => true };
            footerWidth[i] = shown ? ButtonWidth(label) : 0;
            if (!shown)
                continue;
            if (x > 0 && x + footerWidth[i] > fw) {
                x = 0;
                row++;
            }
            footerAt[i] = new(x, top + row * (height + gap));
            x += footerWidth[i] + gap;
        }
        return row + 1;
    }

    private void DrawFooter(SystemConfig config, float y) {
        if (fBack && Button("Back", QuickSetupFaces.Quiet, fx + footerAt[0].X, y + footerAt[0].Y, footerWidth[0]))
            Back(config);
        if (fUndo && Button("Undo", QuickSetupFaces.Quiet, fx + footerAt[1].X, y + footerAt[1].Y, footerWidth[1]))
            Undo(config);
        if (fSkip && Button(SkipText, QuickSetupFaces.Quiet, fx + footerAt[2].X, y + footerAt[2].Y, footerWidth[2]))
            Skip(config);
        if (Button(fNext, QuickSetupFaces.Primary, fx + footerAt[3].X, y + footerAt[3].Y, footerWidth[3], true)) {
            if (Step == QuickSetupStep.Done)
                Finish(config);
            else
                Next(config);
        }
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
