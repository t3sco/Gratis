using System;
using System.Collections.Generic;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static partial class UiMotion {
    private static MapDrawingConfig? policyConfig;
    private static MapDrawingConfig PolicyConfig => policyConfig ?? MapDrawContext.Current.Config;
    public static bool IsGratis => PolicyConfig.IsGratis;
    public static PolicyScope Policy(MapDrawingConfig config) => new(config);
    public readonly struct PolicyScope : IDisposable {
        private readonly MapDrawingConfig? previous;
        public PolicyScope(MapDrawingConfig config) {
            previous = policyConfig;
            policyConfig = config;
        }
        public void Dispose() => policyConfig = previous;
    }

    public static DisabledScope Disabled(string id, bool enabled) => new(id, enabled);
    public readonly struct DisabledScope : IDisposable {
        public DisabledScope(string id, bool enabled) {
            var inherited = ImGui.GetStyle().Alpha;
            var value = Value(ImGui.GetID(id), 24, enabled ? 1 : ImGui.GetStyle().DisabledAlpha,
                InterfaceMotionTokens.Standard, opacity: true);
            ImGui.BeginDisabled(!enabled);
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, inherited * value);
        }
        public void Dispose() {
            ImGui.PopStyleVar();
            ImGui.EndDisabled();
        }
    }
    private struct PageState {
        public int Shown, Requested; public double Changed;
    }

    private static readonly Dictionary<uint, PageState> pages = new(32);
    public static int Page(string id, int requested, out float alpha, out bool interactive) {
        Begin();
        var key = ImGui.GetID(id);
        var now = ImGui.GetTime();
        if (!pages.TryGetValue(key, out var page) || !Full) {
            if (pages.Count == 32)
                pages.Clear();
            page = new() { Shown = requested, Requested = requested, Changed = now - 1 };
        }
        if (page.Requested != requested) {
            page.Requested = requested;
            page.Changed = now;
        }
        var half = Duration(InterfaceMotionTokens.Standard) / 2;
        var age = now - page.Changed;
        if (age >= half)
            page.Shown = requested;
        interactive = page.Shown == requested;
        alpha = !Full || age >= 2 * half ? 1 : age < half
            ? 1 - InterfaceMotionTokens.Ease((float)(age / half), InterfaceCurve.Exit)
            : InterfaceMotionTokens.Ease((float)((age - half) / half), InterfaceCurve.Enter);
        if (alpha < 1)
            presentationActive++;
        pages[key] = page;
        return page.Shown;
    }

    private static string tooltip = "";
    private static string? tooltipMeta;
    private static HintRow[]? tooltipRows;
    private static Vector4 tooltipBounds;
    private static int tooltipSeen = -2, tooltipDrawn = -2;
    private static float tooltipWrap, tooltipScale;
    private static uint tooltipViewport;
    private const uint TooltipId = 0xfac31101;
    public static void TooltipText(string text, float? scale = null) {
        if (!PolicyConfig.IsGratis) {
            ImGui.SetTooltip(text);
            return;
        }
        var s = scale ?? GratisTheme.ChromeScale;
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s,
            ChromeFontRole.Body)?.Push();
        var viewport = ImGui.GetWindowViewport();
        var size = ImGui.CalcTextSize(text, false, 340 * s) + new Vector2(24) * s;
        var p = ImGui.GetMousePos() + new Vector2(18, 24) * s;
        p = Vector2.Clamp(p, viewport.Pos, Vector2.Max(viewport.Pos, viewport.Pos + viewport.Size - size));
        TooltipText(text, new(p.X, p.Y, p.X + size.X, p.Y + size.Y), 340 * s, s);
    }

    public static void TooltipCard(string title, string? meta, HintRow[]? rows, float? scale = null) {
        if (!PolicyConfig.IsGratis) {
            ImGui.SetTooltip(meta == null ? title : title + "\n" + meta);
            return;
        }
        var s = scale ?? GratisTheme.ChromeScale;
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s,
            ChromeFontRole.Body)?.Push();
        var viewport = ImGui.GetWindowViewport();
        var size = HintCard.Measure(title, meta, rows, 340 * s, s) + new Vector2(24) * s;
        var p = ImGui.GetMousePos() + new Vector2(18, 24) * s;
        p = Vector2.Clamp(p, viewport.Pos, Vector2.Max(viewport.Pos, viewport.Pos + viewport.Size - size));
        TooltipCard(title, meta, rows, new(p.X, p.Y, p.X + size.X, p.Y + size.Y), 340 * s, s);
    }

    public static void TooltipCard(string title, string? meta, HintRow[]? rows, Vector4 bounds, float wrap,
        float? scale = null) {
        tooltipMeta = meta;
        tooltipRows = rows;
        TooltipOpen(title, bounds, wrap, scale);
    }

    public static void TooltipText(string text, Vector4 bounds, float wrap, float? scale = null) {
        tooltipMeta = null;
        tooltipRows = null;
        TooltipOpen(text, bounds, wrap, scale);
    }

    private static void TooltipOpen(string text, Vector4 bounds, float wrap, float? scale) {
        Begin();
        tooltip = text;
        tooltipBounds = bounds;
        tooltipWrap = wrap;
        tooltipScale = scale ?? GratisTheme.ChromeScale;
        tooltipViewport = ImGui.GetWindowViewport().ID;
        tooltipSeen = ImGui.GetFrameCount();
        DrawTooltipTail();
    }

    public static void DrawTooltipTail() {
        Begin();
        var current = ImGui.GetFrameCount();
        if (tooltipDrawn == current || tooltip.Length == 0)
            return;
        var open = tooltipSeen == current;
        var alpha = Value(TooltipId, 0, open ? 1 : 0, InterfaceMotionTokens.Micro,
            open ? InterfaceCurve.Enter : InterfaceCurve.Exit, initial: 0, opacity: true);
        if (!open && alpha <= .001f) {
            tooltip = "";
            return;
        }
        tooltipDrawn = current;
        var s = tooltipScale;
        using var style = PopupSurface.Push(PopupFaces.Chrome, scale: s);
        using var font = MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize * s,
            ChromeFontRole.Body)?.Push();
        ImGui.SetNextWindowPos(new(tooltipBounds.X, tooltipBounds.Y));
        ImGui.SetNextWindowSize(new(tooltipBounds.Z - tooltipBounds.X, tooltipBounds.W - tooltipBounds.Y));
        ImGui.SetNextWindowViewport(tooltipViewport);
        ImGui.BeginTooltip();
        using (var motion = new VisualScope(ImGui.GetWindowDrawList(), new(tooltipBounds.X, tooltipBounds.Y), alpha,
            Full ? new Vector2(0, (1 - alpha) * 3 * s) : Vector2.Zero, start: 0)) {
            DrawPopupEdge(s);
            PopupDiagnostics.Capture(open ? "tooltip" : "tooltip-exit", GratisTheme.Radius.Popup * s);
            if (tooltipMeta != null || tooltipRows != null)
                HintCard.Draw(tooltip, tooltipMeta, tooltipRows, tooltipWrap, s);
            else {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + tooltipWrap);
                ImGui.TextUnformatted(tooltip);
                ImGui.PopTextWrapPos();
            }
        }
        ImGui.EndTooltip();
    }

    private static int contentTooltipSeen = -2;
    private static double contentTooltipBegan;
    public static void TooltipContent(Action draw, Vector2 size, float? scale = null) {
        Begin();
        var s = scale ?? GratisTheme.ChromeScale;
        var viewport = ImGui.GetWindowViewport();
        size = Vector2.Min(size, Vector2.Max(Vector2.One, viewport.Size - new Vector2(16 * s)));
        var p = ImGui.GetMousePos() + new Vector2(18, 24) * s;
        p = Vector2.Clamp(p, viewport.Pos + new Vector2(8 * s),
            Vector2.Max(viewport.Pos + new Vector2(8 * s), viewport.Pos + viewport.Size - size - new Vector2(8 * s)));
        var frame = ImGui.GetFrameCount();
        if (contentTooltipSeen < frame - 1) contentTooltipBegan = ImGui.GetTime();
        contentTooltipSeen = tooltipDrawn = frame;
        var alpha = !Full ? 1 : InterfaceMotionTokens.Ease((float)Math.Clamp(
            (ImGui.GetTime() - contentTooltipBegan) / Duration(InterfaceMotionTokens.Micro), 0, 1), InterfaceCurve.Enter);
        using var style = PopupSurface.Push(PopupFaces.Chrome, scale: s);
        ImGui.SetNextWindowPos(p);
        ImGui.SetNextWindowSize(size);
        ImGui.SetNextWindowViewport(viewport.ID);
        ImGui.BeginTooltip();
        try {
            using var motion = new VisualScope(ImGui.GetWindowDrawList(), p, alpha,
                Full ? new Vector2(0, (1 - alpha) * 3 * s) : Vector2.Zero, start: 0);
            DrawPopupEdge(s);
            PopupDiagnostics.Capture("item-tooltip", GratisTheme.Radius.Popup * s);
            draw();
        }
        finally { ImGui.EndTooltip(); }
    }

    public static void ResetPresentation() {
        pages.Clear();
        tooltip = "";
        tooltipSeen = tooltipDrawn = -2;
    }
}
