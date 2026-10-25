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
    public const char Anchor = '\ue03f';
    public const char Bell = '\ue059';
    public const char BookOpen = '\ue05f';
    public const char Check = '\ue06c';
    public const char Crosshair = '\ue0ac';
    public const char House = '\ue0f5';
    public const char Mail = '\ue10f';
    public const char Package = '\ue129';
    public const char Repeat = '\ue146';
    public const char Shield = '\ue158';
    public const char Wrench = '\ue1b1';
    public const char Ticket = '\ue20f';
    public const char Landmark = '\ue23a';
    public const char Swords = '\ue2b4';
    public const char Bed = '\ue2c1';
    public const char Trees = '\ue2f5';
    public const char Fish = '\ue3a6';
    public const char DoorOpen = '\ue3d6';
    public const char Store = '\ue3e4';
    public const char Sparkles = '\ue412';
    public const char ShoppingBasket = '\ue4ea';
    public const char PawPrint = '\ue4f5';
    public const char Pickaxe = '\ue5c6';
    public const char Skull = '\ue221';
    public const char Footprints = '\ue3b9';
    public const char Bird = '\ue3c5';
    public const char Fence = '\ue582';

    public static readonly ushort[] GlyphRanges = [
        Anchor, Anchor,
        Bell, Bell,
        BookOpen, BookOpen,
        Check, Check,
        ArrowUp, ArrowUp,
        Compass, Compass,
        Copy, Copy,
        Crosshair, Crosshair,
        Eye, Eye,
        EyeOff, EyeOff,
        Flag, Flag,
        Info, Info,
        House, House,
        LayoutGrid, LayoutGrid,
        Lock, Lock,
        Unlock, Unlock,
        Mail, Mail,
        MapPin, MapPin,
        Minus, Minus,
        Plus, Plus,
        Package, Package,
        Repeat, Repeat,
        Search, Search,
        Settings, Settings,
        Shield, Shield,
        Wrench, Wrench,
        X, X,
        LocateFixed, LocateFixed,
        Ticket, Ticket,
        Landmark, Landmark,
        Swords, Swords,
        Bed, Bed,
        Trees, Trees,
        Fish, Fish,
        DoorOpen, DoorOpen,
        Store, Store,
        Sparkles, Sparkles,
        ShoppingBasket, ShoppingBasket,
        PawPrint, PawPrint,
        Layers, Layers,
        Pickaxe, Pickaxe,
        Skull, Skull,
        Footprints, Footprints,
        Bird, Bird,
        Fence, Fence,
        0,
    ];

    public static bool Available(char codepoint, float pixels)
    {
        var context = MapDrawContext.Current;
        var handle = context.GetIconFont(TextDrawing.PixelSize(pixels));
        if (handle is null) return false;
        using var scope = handle.Push();
        return context.TryIconGlyphBounds(codepoint, out _);
    }

    public static char ForAction(ToolbarAction action, bool following) => action switch {
        ToolbarAction.Parent => ArrowUp, ToolbarAction.Layers => Layers,
        ToolbarAction.Follow => following ? Lock : Unlock,
        ToolbarAction.CenterPlayer => LocateFixed, ToolbarAction.CenterMap => LayoutGrid,
        ToolbarAction.Search => Search, ToolbarAction.Settings => Settings, ToolbarAction.Close => X,
        _ => Info,
    };

    public static bool Draw(ImDrawListPtr list, Vector2 center, char codepoint, Vector4 color, float pixels)
    {
        var context = MapDrawContext.Current;
        var handle = context.GetIconFont(TextDrawing.PixelSize(pixels));
        if (handle is null) return false;
        using var scope = handle.Push();
        var font = ImGui.GetFont();
        if (!context.TryIconGlyphBounds(codepoint, out var bounds)) return false;
        pixels = font.FontSize;
        var ratio = pixels / font.FontSize;
        var position = TextDrawing.Snap(center - new Vector2(bounds.X + bounds.Z, bounds.Y + bounds.W) * (.5f * ratio));
        DrawBinding.PushFontTexture(list);
        list.AddText(font, pixels, position, ImGui.GetColorU32(color), codepoint.ToString());
        DrawBinding.PopTexture(list);
        return true;
    }
}
