using System;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Gratis.Data;
using Gratis.Rendering;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Windows;

public sealed class SettingsTheme
{
    private readonly bool active;
    private readonly IDisposable? font;
    private SettingsTheme(SystemConfig config)
    {
        active = config.IsGratis;
        if (!active) return;
        font = Font(14);
        var scale = Scale;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 6) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(12, 6) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, GratisTheme.CardRadius * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, GratisTheme.CardRadius * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, GratisTheme.CardRadius * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 6 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 8 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 4 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.DisabledAlpha, .7f);
        var surface = GratisTheme.Surface with { W = 1 };
        var hover = new Vector4(1, 1, 1, .06f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, surface);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, surface);
        ImGui.PushStyleColor(ImGuiCol.Border, GratisTheme.Border);
        ImGui.PushStyleColor(ImGuiCol.BorderShadow, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Primary);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, GratisTheme.Secondary);
        ImGui.PushStyleColor(ImGuiCol.TitleBg, surface);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, surface);
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, surface);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(1, 1, 1, .025f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, hover);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(1, 1, 1, .08f));
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1, 1, 1, .10f));
        ImGui.PushStyleColor(ImGuiCol.Header, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, hover);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, hover);
        ImGui.PushStyleColor(ImGuiCol.Separator, GratisTheme.Border);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, new Vector4(1, 1, 1, .18f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, new Vector4(1, 1, 1, .3f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, new Vector4(1, 1, 1, .4f));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, config.AccentColor);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, config.AccentColor);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, config.AccentColor);
        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, hover);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, config.AccentColor);
    }

    public static SettingsTheme Push(SystemConfig config) => new(config);
    public void Pop()
    {
        if (!active) return;
        ImGui.PopStyleColor(30);
        ImGui.PopStyleVar(14);
        font?.Dispose();
    }
    public static float Scale => MapDrawContext.Current.GlobalScale;
    public static IDisposable? Font(float size, ChromeFontRole role = ChromeFontRole.Body) =>
        MapDrawContext.Current.GetChromeFont(size * Scale, role)?.Push();

    public static void Helper(string text)
    {
        using var font = Font(12);
        ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Secondary);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    public static bool Toggle(string id, ref bool value, Vector4 accent, string? label = null)
    {
        var scale = Scale;
        var start = ImGui.GetCursorScreenPos();
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
        var width = 36 * scale + (label is null ? 0 : 8 * scale + ImGui.CalcTextSize(label).X);
        var changed = ImGui.Button(id, new Vector2(width, 20 * scale));
        ImGui.PopStyleVar();
        if (changed) value = !value;
        var list = ImGui.GetWindowDrawList();
        var end = start + new Vector2(36, 20) * scale;
        list.AddRectFilled(start, end, ImGui.GetColorU32(value ? accent : new Vector4(1, 1, 1, .12f)), 10 * scale);
        list.AddRect(start, end, ImGui.GetColorU32(GratisTheme.Border), 10 * scale);
        list.AddCircleFilled(start + new Vector2(value ? 26 : 10, 10) * scale, 6 * scale,
            ImGui.GetColorU32(GratisTheme.Primary));
        if (label is not null) list.AddText(start + new Vector2(44 * scale, (20 * scale - ImGui.GetFontSize()) / 2),
            ImGui.GetColorU32(GratisTheme.Primary), label);
        return changed;
    }

    public static bool Slider(string id, ref float value, float min, float max, string format, Vector4 accent, bool integer = false)
    {
        ImGui.BeginGroup();
        using var font = Font(12, ChromeFontRole.Coordinates);
        var scale = Scale;
        var width = MathF.Max(60 * scale, ImGui.CalcItemWidth());
        var readout = MathF.Min(width * .45f, MathF.Max(72 * scale, ImGui.CalcTextSize(Format(max, format)).X + 8 * scale));
        var trackWidth = width - readout - 8 * scale;
        var start = ImGui.GetCursorScreenPos();
        ImGui.SetNextItemWidth(trackWidth);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, GratisTheme.Primary);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
        bool changed;
        if (integer) {
            var number = (int)value;
            changed = ImGui.SliderInt(id, ref number, (int)min, (int)max, format);
            value = number;
        }
        else changed = ImGui.SliderFloat(id, ref value, min, max, format);
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(6);
        var height = ImGui.GetItemRectSize().Y;
        var list = ImGui.GetWindowDrawList();
        if (!ImGui.IsItemActive() || !ImGui.GetIO().WantTextInput) {
            list.AddRectFilled(start + new Vector2(2 * scale), start + new Vector2(trackWidth - 2 * scale, height - 2 * scale),
                ImGui.ColorConvertFloat4ToU32(GratisTheme.Surface with { W = 1 }));
            var left = start + new Vector2(6 * scale, height / 2);
            var right = start + new Vector2(trackWidth - 6 * scale, height / 2);
            var knob = Vector2.Lerp(left, right, Math.Clamp((value - min) / (max - min), 0, 1));
            list.AddLine(left, right, ImGui.GetColorU32(new Vector4(1, 1, 1, .14f)), 2 * scale);
            list.AddLine(left, knob, ImGui.GetColorU32(accent), 2 * scale);
            list.AddCircleFilled(knob, 4 * scale, ImGui.GetColorU32(accent));
        }
        ImGui.SameLine(0, 8 * scale);
        var text = Format(value, format);
        ImGui.Dummy(new Vector2(readout, height));
        list.AddText(start + new Vector2(trackWidth + 8 * scale, (height - ImGui.GetFontSize()) / 2),
            ImGui.GetColorU32(GratisTheme.Primary), text);
        ImGui.EndGroup();
        return changed;
    }

    private static string Format(float value, string format) => Regex.Replace(format, @"%(?:\.(\d+))?([fd])", match =>
        value.ToString(match.Groups[2].Value == "d" ? "0" : "F" + (match.Groups[1].Success ? match.Groups[1].Value : "2"), CultureInfo.InvariantCulture));
}
