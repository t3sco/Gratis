using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public static class GratisIcons
{
    public const char Lock = '\ue10b';
    public const char Unlock = '\ue10c';
    public const char Layers = '\ue529';
    public const char LocateFixed = '\ue1db';
    public const char LayoutGrid = '\ue0ff';
    public const char Search = '\ue151';
    public const char Settings = '\ue154';
    public const char X = '\ue1b2';
    public const char ArrowUp = '\ue04a';
    public const char Plus = '\ue13d';
    public const char Minus = '\ue11c';
    public const char MapPin = '\ue111';
    public const char Flag = '\ue0d1';
    public const char Eye = '\ue0ba';
    public const char EyeOff = '\ue0bb';
    public const char Compass = '\ue09b';
    public const char Copy = '\ue09e';
    public const char Info = '\ue0f9';

    public static readonly ushort[] GlyphRanges = [
        ArrowUp, ArrowUp,
        Compass, Compass,
        Copy, Copy,
        Eye, Eye,
        EyeOff, EyeOff,
        Flag, Flag,
        Info, Info,
        LayoutGrid, LayoutGrid,
        Lock, Lock,
        Unlock, Unlock,
        MapPin, MapPin,
        Minus, Minus,
        Plus, Plus,
        Search, Search,
        Settings, Settings,
        X, X,
        LocateFixed, LocateFixed,
        Layers, Layers,
        0,
    ];

    public static char ForAction(ToolbarAction action, bool following) => action switch {
        ToolbarAction.Parent => ArrowUp, ToolbarAction.Layers => Layers,
        ToolbarAction.Follow => following ? Lock : Unlock,
        ToolbarAction.CenterPlayer => LocateFixed, ToolbarAction.CenterMap => LayoutGrid,
        ToolbarAction.Search => Search, ToolbarAction.Settings => Settings, ToolbarAction.Close => X,
        _ => Info,
    };

    public static bool Draw(ImDrawListPtr list, Vector2 center, char codepoint, Vector4 color, float pixels)
    {
        var handle = MapDrawContext.Current.GetIconFont(pixels);
        if (handle is null) return false;
        using var scope = handle.Push();
        var font = ImGui.GetFont();
        if (!DrawBinding.TryGlyphBounds(font, codepoint, out var bounds)) return false;
        var ratio = pixels / ImGui.GetFontSize();
        var position = center - new Vector2(bounds.X + bounds.Z, bounds.Y + bounds.W) * (.5f * ratio);
        DrawBinding.PushFontTexture(list);
        list.AddText(font, pixels, position, ImGui.GetColorU32(color), codepoint.ToString());
        DrawBinding.PopTexture(list);
        return true;
    }
}
