using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
#if !GRATIS_PREVIEW
using Gratis.Compat.Configuration;
#endif

namespace Gratis.Data;

public enum CenterTarget {
    [Description("Disabled")]
    Disabled = 0,

    [Description("Player")]
    Player = 1,

    [Description("Map")]
    Map = 2,
}

[Flags]
public enum FadeMode {
    [Description("Always")]
    Always = 1 << 0,

    [Description("When Moving")]
    WhenMoving = 1 << 2,

    [Description("When Focused")]
    WhenFocused = 1 << 3,

    [Description("When Unfocused")]
    WhenUnFocused = 1 << 4,
}

/// <summary>
/// Tells a first-ever install from a returning one by what is in the plugin config directory, so a deleted or
/// damaged System.config.json never makes a returning user look new. Pure, so the preview harness can test it.
/// </summary>
public static class FirstRun {
    /// <summary>Written on its own the moment Quick setup is finished or skipped.</summary>
    public const string DoneMarker = "quick-setup.done";

    /// <summary>A fresh install has nothing at all in the config directory; any file or folder is a past run.</summary>
    public static bool IsFreshInstall(IEnumerable<string> entries) => !entries.Any(e => !string.IsNullOrWhiteSpace(e));

    public static bool HasDoneMarker(IEnumerable<string> entries) => entries.Any(e => string.Equals(e, DoneMarker,
        StringComparison.OrdinalIgnoreCase));
}

public class SystemConfig : Gratis.Rendering.MapDrawingConfig {
    public Gratis.Rendering.MapColourSettings MapColour = new();

    public bool UseLinearZoom = false;
    public float ZoomSpeed = 0.085f;
    public bool ShowMiscTooltips = true;
    public bool HideWithGameGui = true;
    public bool HideBetweenAreas = true;
    public bool HideInCombat = false;
    public bool KeepOpen = false;
    public bool FollowOnOpen = true;
    public bool AutoSwitchOnZoneChange = true;
    public bool OnlyIfViewingCurrentZone = true;
    public bool SwitchInDuties = true;
    public CenterTarget CenterOnOpen = CenterTarget.Player;
    public bool ShowRadar = false;
    public bool ShowRadarInDuties = false;
    public Vector4 RadarColor = ColorVector(KnownColor.Gray) with { W = 0.10f };
    public Vector4 RadarOutlineColor = ColorVector(KnownColor.Gray) with { W = 0.30f };
    public bool HideWindowBackground = true;
    public bool BlurBackground = false;
    public float BlurStrength = 0.5f;
    public bool EnableShiftDragMove = false;
    public bool LockWindow = false;
    public bool MoveHandle = true;
    public bool AltDragMovesWindow = true;
    public bool MoveHandleSnap = true;
    public bool ClassicMoveHandle = false;
    public float FadePercent = 0.81f;
    public FadeMode FadeMode = FadeMode.WhenUnFocused;
    public Vector2 WindowPosition = new(1024.0f, 700.0f);
    public Vector2 WindowSize = new(500.0f, 500.0f);
    public bool AlwaysShowToolbar = false;
    public bool ShowToolbarOnHover = true;
    public bool AcceptedSpoilerWarning = false;
    public bool CenterOnFlag = true;
    public bool CenterOnGathering = true;
    public bool CenterOnQuest = true;
    public bool LockCenterOnMap = false;
    public bool ZoomLocked = false;
    public bool ShowPlayers = true;
    public bool SetFlagOnFateClick = false;
    public float MapScale = 1.0f;
    public bool AutoZoom = false;
    public bool ShowRegionLabel = true;
    public bool ShowMapLabel = true;
    public bool ShowAreaLabel = true;
    public bool ShowSubAreaLabel = true;
    public bool NoFocusOnAppear = false;
    public float LargeAreaTextScale = 1.0f;
    public float SmallAreaTextScale = 0.5f;
    public float LargeAreaTextOpacity = 0.30f;
    public float SmallAreaTextOpacity = 1.0f;
    public bool ShowTextLabels = true;
    public bool ShowFogOfWar = false;
    public bool ScaleTextWithZoom = false;
    public float AutoZoomScaleFactor = 0.38f;

    public bool SuppressNativeMapOpenSound = false;

#if !GRATIS_PREVIEW
    private const string FileName = "System.config.json";

    private static string[]? ConfigEntries() {
        try {
            var directory = Service.PluginInterface.ConfigDirectory.FullName;
            return Directory.Exists(directory)
                ? Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName).OfType<string>().ToArray() : [];
        }
        catch (Exception) {
            return null;
        }
    }

    public static SystemConfig Load() {
        // LoadConfigFile writes a default file straight away, so the directory is read before it. A directory that
        // cannot be read counts as a returning install: better to skip Quick setup than to show it again.
        var entries = ConfigEntries();
        var fresh = entries is not null && FirstRun.IsFreshInstall(entries);
        var config = Service.PluginInterface.LoadConfigFile<SystemConfig>(FileName);
        if (fresh) {
            config.MarkFreshInstall();
            Service.PluginInterface.SaveConfigFile(FileName, config);
        }
        else if (entries is null || FirstRun.HasDoneMarker(entries))
            config.QuickSetupDone = true;
        return config;
    }

    /// <summary>Best effort: a marker that cannot be written still leaves the flag in the settings file.</summary>
    public static void WriteDoneMarker() {
        try {
            File.WriteAllText(Path.Combine(Service.PluginInterface.GetPluginConfigDirectory(), FirstRun.DoneMarker), "");
        }
        catch (Exception e) {
            Service.Log.Warning(e, "Could not write the Quick setup marker.");
        }
    }

    public static void Save() => Service.PluginInterface.SaveConfigFile(FileName, System.SystemConfig);
#endif
}