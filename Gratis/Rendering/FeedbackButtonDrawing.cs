using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class FeedbackLink {
    private const string Repository = "https://github.com/t3sco/Gratis";
    public static Action<string>? Open { get; set; }
    public static Func<string>? BuildReport { get; set; }

    public static string Url() {
        var version = typeof(FeedbackLink).Assembly.GetName().Version?.ToString() ?? "unknown";
        var body = $"Gratis version: {version}\n\n**What happened?**\n\n\n**What did you expect?**\n\n\n"
            + "If it looks like a bug, `/gratis report` saves a file with details you can attach here.";
        return $"{Repository}/issues/new?body={Uri.EscapeDataString(body)}";
    }
}

public static class FeedbackButtonDrawing {
    private const string Popup = "Gratis_Feedback";
    private static string draft = "";
    private static bool includeReport, sentHandled;
    private static double closeAt;
    public static Vector4 LastBounds { get; private set; }
    public static bool PopupOpen { get; private set; }

    public static void Draw() {
        LastBounds = Vector4.Zero;
        PopupOpen = false;
        var context = MapDrawContext.Current;
        if (!context.Config.IsGratis || !context.Config.ShowFeedbackButton || context.ContextMenu.CapturesInput)
            return;
        using var alpha = new ChromeVisibility.AlphaScope(true, context.Chrome.ControlsAlpha(context.Config));
        if (!alpha.Visible)
            return;
        var s = GratisTheme.ChromeScale;
        var size = new Vector2(GratisTheme.ControlHeight * s);
        var bounds = ChromeLayout.Place("feedback", size, ChromeAnchor.BottomLeft);
        if (bounds == Vector4.Zero)
            return;
        LastBounds = bounds;
        var pos = ChromeLayout.Position(bounds);
        var list = ImGui.GetWindowDrawList();
        MapWindowDrawing.DrawSurface(list, pos, pos + size, GratisTheme.Radius.Card * s);
        ImGui.SetCursorScreenPos(pos);
        if (ChromeControls.IconButton("##feedback", GratisIcons.MessageSquare, "Send feedback or report a bug", size,
                windowBounds: MapToolbarDrawing.WindowBounds, radius: GratisTheme.Radius.Card * s)) {
            FeedbackSender.Reset();
            sentHandled = false;
            ImGui.OpenPopup(Popup);
        }
        DrawPopup(bounds);
    }

    private static void DrawPopup(Vector4 anchor) {
        var context = MapDrawContext.Current;
        var s = GratisTheme.ChromeScale;
        using var style = PopupSurface.Push(PopupFaces.Chrome);
        using var font = context.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Body)?.Push();
        var (min, max) = ChromeLayout.PopoverBounds();
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var field = 96 * s;
        var height = 2 * ImGui.GetStyle().WindowPadding.Y + ImGui.GetTextLineHeight() * 3 + field
            + ImGui.GetFrameHeight() + GratisTheme.ControlHeight * s + 5 * spacing;
        var size = Vector2.Min(new Vector2(320 * s, MathF.Ceiling(height)), max - min);
        var x = Math.Clamp(anchor.X, min.X, MathF.Max(min.X, max.X - size.X));
        var y = Math.Clamp(anchor.Y - GratisTheme.Gap * s - size.Y, min.Y, MathF.Max(min.Y, max.Y - size.Y));
        var b = new Vector4(x, y, x + size.X, y + size.Y);
        var popup = UiMotion.Popup(Popup, !ImGui.IsKeyPressed(ImGuiKey.Escape, false), new(b.X, b.Y), size,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (!popup.Visible)
            return;
        try {
            PopupOpen = popup.Interactive;
            context.Chrome.Reveal(ImGui.GetTime());
            if (popup.Interactive)
                ImGui.SetNextFrameWantCaptureKeyboard(true);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(1, 1, 1, .055f));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(7, 5) * s);
            using (context.GetChromeFont(GratisTheme.TextSize * s, ChromeFontRole.Title)?.Push())
                ImGui.TextUnformatted("Send feedback");
            var fieldPos = ImGui.GetCursorScreenPos();
            ImGui.InputTextMultiline("##feedback-text", ref draft, FeedbackSender.MaxLength, new Vector2(-1, field));
            if (draft.Length == 0 && !ImGui.IsItemActive())
                ImGui.GetWindowDrawList().AddText(fieldPos + new Vector2(8, 6) * s,
                    ImGui.GetColorU32(GratisTheme.Secondary with { W = .6f }), "What's on your mind?");
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
            ImGui.Checkbox("Include a diagnostic report", ref includeReport);
            var state = FeedbackSender.State;
            var (note, colour) = state switch {
                FeedbackState.Sending => ("Sending...", GratisTheme.Secondary),
                FeedbackState.Sent => ("Sent. Thank you!", GratisTheme.Secondary),
                FeedbackState.Failed => (FeedbackSender.Error, GratisTheme.Objective),
                _ => ("Settings and technical details only, no character name or chat. "
                    + "Nothing is sent until you press Send.", GratisTheme.Secondary),
            };
            ImGui.PushStyleColor(ImGuiCol.Text, colour);
            ImGui.PushTextWrapPos(ImGui.GetWindowWidth() - 12 * s);
            ImGui.TextUnformatted(note);
            ImGui.PopTextWrapPos();
            ImGui.PopStyleColor();
            if (state == FeedbackState.Sent) {
                if (!sentHandled) {
                    sentHandled = true;
                    draft = "";
                    closeAt = ImGui.GetTime() + 1.5;
                }
                else if (ImGui.GetTime() >= closeAt)
                    ImGui.CloseCurrentPopup();
            }
            var ready = draft.Trim().Length > 0 && FeedbackSender.CanSend;
            var send = ActionButton.Draw("Send", "Send", new(ActionFaces.Send, Icon: GratisIcons.Check, Enabled: ready));
            if (send && ready)
                FeedbackSender.Send(draft, includeReport ? FeedbackLink.BuildReport?.Invoke() : null);
            ImGui.SameLine();
            if (ActionButton.Draw("Open on GitHub", "Open on GitHub", new(ActionFaces.Quiet)))
                FeedbackLink.Open?.Invoke(FeedbackLink.Url());
        }
        finally { popup.Dispose(); }
    }
}
