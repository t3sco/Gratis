using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

internal static class FrameScopes
{
    public readonly struct Style : IDisposable
    {
        private readonly bool pushed;
        public Style(ImGuiStyleVar variable, float value, bool enabled)
        {
            pushed = enabled;
            if (enabled) ImGui.PushStyleVar(variable, value);
        }
        public void Dispose() { if (pushed) ImGui.PopStyleVar(); }
    }
    public static Style PushStyle(ImGuiStyleVar variable, float value, bool enabled) => new(variable, value, enabled);
    public readonly struct ChildScope : IDisposable
    {
        public bool Visible { get; }
        public ChildScope(string id, Vector2 size, ImGuiWindowFlags flags) => Visible =
#if GRATIS_PREVIEW
            ImGui.BeginChild(id, size, ImGuiChildFlags.None, flags);
#else
            ImGui.BeginChild(id, size, false, flags);
#endif
        public void Dispose() => ImGui.EndChild();
    }
    public static ChildScope Child(string id, Vector2 size, ImGuiWindowFlags flags) => new(id, size, flags);
    public readonly struct GroupScope : IDisposable
    {
        public GroupScope() => ImGui.BeginGroup();
        public void Dispose() => ImGui.EndGroup();
    }
    public static GroupScope Group() => new();
}
