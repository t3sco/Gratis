using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gratis.Classes;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal sealed partial class ScriptedFrameDriver : IMapWindowHost, IMapRendererHost, IDisposable {
    internal static readonly JsonSerializerOptions Json = new() {
        IncludeFields = true, PropertyNameCaseInsensitive = true, WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private readonly nint native;
    private readonly MapDrawContext previous;
    private readonly DrawTexture map;
    private Vector2? textureSizeOverride;
    private MapContentBounds? scannedContent, contentOverride;
    private bool contentReady = true;
    private (ulong Map, long Session)? seededManualView;
    public bool TextureReady { get; private set; } = true;
    public Vector2? ViewportOverride { get; private set; }
    public long ViewSession { get; private set; }
    public bool InstanceMap => script.InstanceMap;
    private readonly Action<ToolbarAction> activate;
    private readonly Action layers;
    private bool focused = true, left, right, shift, control, alt;
    private Vector2 mouse = new(-1000), player;
    private float heading;
    private float? cameraYaw;
    private float? nativeConeDegrees;
    public PlayerOrientation PlayerOrientation { get; } = new();
    private int redraws = 1;
    private bool started;
    private readonly FrameScript script;
    private ImGuiKey[] pressedKeys = [];
    public PaletteFixture? PlaceSource { get; private set; }
    public PaletteOverlay? Palette => PlaceSource?.Overlay;
    public Textures Textures { get; } = new();
    public PreviewContext Context { get; }
    public MapWindowFrame Window { get; } = new();
    public MapRendererFrame Renderer { get; }
    public MapFrameTrace Trace { get; } = new();
    public SystemConfig Config { get; }
    public List<MarkerInfo> Markers { get; } = [];
    public int Width { get; }
    internal bool RepeatToolbarForVerification { get; set; }
    public int Height { get; }
    public nint MapDrawList { get; private set; }
    public double Now { get; private set; }
    public int FrameId => ImGui.GetFrameCount();
    public uint MapId { get; private set; }
    public uint TerritoryId { get; private set; }
    public ulong MapKey => ((ulong)TerritoryId << 32) | MapId;
    public Vector2? PlayerPosition => player;
    public bool SettingsOpen { get; private set; }
    public bool ShouldFade => Config.FadeMode.HasFlag(FadeMode.Always)
        || Config.FadeMode.HasFlag(FadeMode.WhenFocused) && focused
        || Config.FadeMode.HasFlag(FadeMode.WhenUnFocused) && !focused
        || Config.FadeMode.HasFlag(FadeMode.WhenMoving) && moving;
    private bool moving;
    public bool PlayerMoving => moving;
    public bool MapFocused => focused;
    private SubZoneLocation? subZone;
    public SubZoneLocation? SubZone => (subZone ?? script.SubZone) is { } place && place.MapId == MapId ? place : null;
    public string Title => script.Title;
    public MoveWorkArea MoveWorkArea => new(script.ScreenOrigin, script.ScreenOrigin + new Vector2(Width, Height));
    public MoveWorkArea MoveWorkAreaAt(Vector2 point) => MoveWorkArea;
    public int PositionSaves { get; private set; }
    public int ContextMenuCalls { get; private set; }
    public void SaveWindowPosition() => PositionSaves++;
    public ImGuiWindowFlags Flags { get; set; } = ImGuiWindowFlags.NoSavedSettings |
        ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar;

    public unsafe ScriptedFrameDriver(SystemConfig? config = null, FrameScript? scene = null, bool benchmark = false) {
        var startup = benchmark ? global::System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        script = scene ?? new FrameScript();
        Width = script.Width;
        Height = script.Height;
        if (Width < 320 || Height < 240 || Width > 8192 || Height > 8192 || (long)Width * Height > 16777216)
            throw new ArgumentException("Display must be 320x240 to 8192x8192 with at most 16 megapixels.");
        if (!float.IsFinite(script.Dpi) || script.Dpi < .5f || script.Dpi > 3 || !float.IsFinite(script.MapSizeFactor)
            || script.MapSizeFactor <= 0)
            throw new ArgumentException("DPI must be .5 to 3 and map size factor must be positive.");
        Config = config ?? new SystemConfig {
            AcceptedSpoilerWarning = true, FadeMode = 0,
            WindowPosition = new Vector2(32), WindowSize = new Vector2(640, 480),
        };
        if (!float.IsFinite(Config.WindowSize.X) || !float.IsFinite(Config.WindowSize.Y) || Config.WindowSize.X < 64
            || Config.WindowSize.Y < 64)
            throw new ArgumentException("Window size must be finite and at least 64x64.");
        TerritoryId = script.TerritoryId;
        MapId = script.MapId;
        player = script.Player;
        heading = script.Heading;
        cameraYaw = script.CameraYaw;
        nativeConeDegrees = script.NativeConeDegrees;
        previous = MapDrawContext.Current;
        native = ImGuiErrors.CreateContext();
        var io = ImGui.GetIO();
        if (!float.IsFinite(script.ScreenOrigin.X) || !float.IsFinite(script.ScreenOrigin.Y)
            || script.ScreenOrigin.X < 0 || script.ScreenOrigin.Y < 0)
            throw new ArgumentException("Headless ScreenOrigin must be finite and nonnegative; negative desktop bounds "
                + "are covered by pure geometry tests.");
        io.DisplaySize = script.ScreenOrigin + new Vector2(Width, Height);
        io.DisplayFramebufferScale = Vector2.One;
        io.DeltaTime = 1f / 60;
        io.NativePtr->IniFilename = null;
        io.NativePtr->LogFilename = null;
        io.ConfigInputTrickleEventQueue = false;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        ImGui.StyleColorsDark();
        Context = new PreviewContext(Textures, Config) {
            Trace = Trace,
            DpiScale = script.Dpi,
            SizeFactor = script.MapSizeFactor,
            ColourPreset = Config.MapColour.Preset
        };
        MapDrawContext.Current = Context;
        var fontStart = benchmark ? global::System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        Context.BuildFonts(Path.Combine(AppContext.BaseDirectory, "assets", "NotoSans-Regular.ttf"));
        if (benchmark) FontRegistrationMs = global::System.Diagnostics.Stopwatch.GetElapsedTime(fontStart).TotalMilliseconds;
        fontStart = benchmark ? global::System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        io.Fonts.GetTexDataAsRGBA32(out nint pixels, out var width, out var height, out _);
        if (benchmark) FontAtlasMs = global::System.Diagnostics.Stopwatch.GetElapsedTime(fontStart).TotalMilliseconds;
        var atlas = new byte[checked(width * height * 4)];
        Marshal.Copy(pixels, atlas, 0, atlas.Length);
        io.Fonts.SetTexID(Textures.Add(width, height, atlas));
        var id = script.SmallInstance ? SmallInstanceVerification.CreateTexture(Textures) : script.Texture is null
            ? Terrain.Create(Textures, 2048) : Textures.Load(script.Texture);
        var texture = Textures.Get(id);
        scannedContent = script.SmallInstance || script.Texture is not null
            ? MapContent.Scan(texture.Pixels, texture.Width, texture.Height) : null;
        if (!Config.MapColour.Parameters.IsIdentity)
            id = Textures.Add(texture.Width, texture.Height,
                MapColour.Apply(texture.Pixels, Config.MapColour.Parameters));
        map = new DrawTexture(id, new Vector2(texture.Width, texture.Height));
        Renderer = new MapRendererFrame(this) { DrawOffset = script.DrawOffset ?? Vector2.Zero };
        if (script.Palette)
            PlaceSource = new PaletteFixture(this);
        if (script.QuestLog)
            Context.QuestSource = new QuestLogFixture(this);
        activate = Activate;
        layers = DrawLayers;
        foreach (var marker in script.Markers) {
            Markers.Add(new MarkerInfo {
                Position = marker.World, Offset = Vector2.Zero, Scale = 1,
                Source = marker.Source, IconId = marker.Icon, MarkerType = marker.Kind, IsObjective = marker.Objective,
                IsRelevant = marker.Relevant, DataId = (uint)Markers.Count + 1
            });
        }
        if (benchmark) StartupMs = global::System.Diagnostics.Stopwatch.GetElapsedTime(startup).TotalMilliseconds;
    }

    public FrameSnapshot Step(ScriptFrame frame, bool snapshot = true) {
        if (!double.IsFinite(frame.Time) || started && frame.Time < Now)
            throw new ArgumentException("Script time must be finite and nondecreasing.");
        if (frame.Redraws is < 1 or > 2)
            throw new ArgumentException("Redraws must be 1 or 2.");
        var io = ImGui.GetIO();
        io.DeltaTime = (float)Math.Max(.000001, started ? frame.Time - Now : frame.Time);
        Now = frame.Time;
        started = true;
        if (frame.IsPvP is { } pvp) {
            Context.PvP = pvp;
            Context.SynchronizePvp();
        }
        mouse = frame.Mouse ?? mouse;
        left = frame.Left ?? left;
        right = frame.Right ?? right;
        shift = frame.Shift ?? shift;
        control = frame.Control ?? control;
        alt = frame.Alt ?? alt;
        focused = frame.Focused ?? focused;
        SettingsOpen = frame.SettingsOpen ?? SettingsOpen;
        moving = frame.Moving ?? (frame.Player is { } next && player != next);
        if (frame.MapKey)
            Window.Ghost.Wake(Now);
        player = frame.Player ?? player;
        heading = frame.Heading ?? heading;
        cameraYaw = frame.CameraYaw ?? cameraYaw;
        nativeConeDegrees = frame.NativeConeDegrees ?? nativeConeDegrees;
        redraws = frame.Redraws ?? redraws;
        MapId = frame.MapId ?? MapId;
        TerritoryId = frame.TerritoryId ?? TerritoryId;
        if (frame.TerritoryId.HasValue)
            ViewSession++;
        TextureReady = frame.TextureReady ?? TextureReady;
        textureSizeOverride = frame.TextureSize ?? textureSizeOverride;
        contentReady = frame.ContentReady ?? contentReady;
        contentOverride = frame.Content ?? contentOverride;
        ViewportOverride = frame.ViewportSize ?? ViewportOverride;
        if (frame.Dpi is { } dpi)
            Context.DpiScale = dpi;
        subZone = frame.SubZone ?? subZone;
        if (frame.UnixTime is { } unix)
            Context.ClockUnixSeconds = unix;
        if (frame.QuestLink is { } quest)
            Renderer.BeginQuestSpotlight(quest.Target, quest.MapId, quest.SameMap, quest.Icon);
        if (frame.Size is { } size)
            Config.WindowSize = size;
        if (!script.AutoFit && !script.SmallInstance && seededManualView != (MapKey, ViewSession)) {
            Renderer.UseManualView("scripted explicit viewport", true);
            seededManualView = (MapKey, ViewSession);
        }
        io.AddMousePosEvent(mouse.X, mouse.Y);
        io.AddMouseButtonEvent(0, left);
        io.AddMouseButtonEvent(1, right);
        io.AddMouseWheelEvent(0, frame.Wheel);
        io.AddKeyEvent(ImGuiKey.Escape, frame.Escape);
        io.AddKeyEvent(ImGuiKey.ModShift, shift);
        io.AddKeyEvent(ImGuiKey.ModCtrl, control);
        io.AddKeyEvent(ImGuiKey.ModAlt, alt);
        foreach (var key in pressedKeys)
            io.AddKeyEvent(key, false);
        pressedKeys = frame.Keys ?? [];
        foreach (var key in pressedKeys)
            io.AddKeyEvent(key, true);
        if (frame.Text is { } text)
            io.AddInputCharactersUTF8(text);
        Context.Blurs.Clear();
        ImGui.NewFrame();
        BenchmarkLabelsDrawn = 0;
        Context.Blur.BeginFrame(FrameId);
        if (BenchmarkMapVisible)
            DrawMapWindow();
        DrawBenchmarkWindows();
        DrawPreviewOverlay?.Invoke();
        ImGui.Render();
        return snapshot ? Snapshot() : default;
    }

    private void DrawMapWindow() {
        Window.PreDraw(this);
        ImGui.SetNextWindowPos(Config.WindowPosition);
        ImGui.SetNextWindowSize(Config.WindowSize);
        if (!focused) {
            ImGui.SetNextWindowPos(script.ScreenOrigin + new Vector2(Width - 200, 0));
            ImGui.SetNextWindowSize(new Vector2(190, 100));
            ImGui.SetNextWindowFocus();
            ImGui.Begin("script keyboard focus", ImGuiWindowFlags.NoSavedSettings);
            ImGui.End();
            ImGui.SetNextWindowPos(Config.WindowPosition);
            ImGui.SetNextWindowSize(Config.WindowSize);
        }
        else if (!PreserveWindowZOrder && !BenchmarkSettingsVisible && !Context.ContextMenu.IsOpen && !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId
            | ImGuiPopupFlags.AnyPopupLevel))
            ImGui.SetNextWindowFocus();
        var neutral = Config.IsGratis && Config.NeutralFrame;
        if (neutral) {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, Config.FlatCornerRadius * Context.GlobalScale);
        }
        if (Config.UseCustomWindowChrome)
            Flags |= ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground;
        else
            Flags &= ~(ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground);
        if (Config.LockWindow || Config.IsGratis || Config.ClassicMoveHandle)
            Flags |= ImGuiWindowFlags.NoMove;
        ImGui.Begin("###GratisMapWindow", Flags);
        if (neutral)
            ImGui.PopStyleVar(3);
        Window.Draw(this);
        ImGui.End();
    }

    public FrameSnapshot Snapshot() {
        var quests = 0;
        var unknownObjectives = 0;
        foreach (var marker in Trace.DrawnMarkers) {
            if (MarkerIcon.TryQuest(marker.IconId, out _))
                quests++;
            if (marker.IconId == 61000 && FlatMarkerDrawing.Category(marker) == MarkerCategory.Objective)
                unknownObjectives++;
        }
        return new FrameSnapshot(Now, FrameId, Config.MapScale, Renderer.DrawOffset, Renderer.DrawPosition,
            Renderer.ViewportSize, Context.Chrome.Progress, Renderer.viewportMotion.Follow.State, Window.Dragging,
            Window.HoveredFlags, Trace.MotionAdvances, Trace.TexturePasses, Trace.WidgetCount, Trace.IdCollisions,
            Trace.DuplicateChrome, Context.Blur.Blurred, Context.Blur.Refused,
            Context.Blur.SurfaceStatus(GlassSurface.Toolbar), Context.Blur.SurfaceStatus(GlassSurface.Title),
            Context.Blur.SurfaceStatus(GlassSurface.Coordinates), Trace.DrawnMarkers.Count, quests, unknownObjectives,
            MapId, Renderer.viewportMotion.PendingZoom, Context.Blur.Count(BlurRefusal.Clip),
            Context.Blur.Count(BlurRefusal.Circular), Context.Blur.Count(BlurRefusal.Alpha),
            Context.Blur.Count(BlurRefusal.Config), Context.Blur.Count(BlurRefusal.Host), Config.WindowPosition,
            Config.WindowSize, Window.MoveHandle.Dragging, Window.MoveHandle.Visibility,
            Window.MoveHandle.Geometry.Center, Renderer.Spotlight.Active, Renderer.Spotlight.Opacity,
            Renderer.Spotlight.FitZoom, Renderer.Spotlight.Decision) {
            QuestLogOpen = Context.QuestLog.IsOpen, QuestLogPinned = Config.QuestLogPinned,
            QuestSearchOpen = Context.QuestLog.SearchOpen, QuestDetailId = Context.QuestLog.DetailQuestId,
            QuestDetailBounds = Context.QuestLog.DetailBounds,
            GhostOpacity = Window.Ghost.Opacity,
            GhostClickThrough = Window.Ghost.ClickThrough,
            GhostFlags = Flags.ToString(),
            SubZoneOpacity = Window.ZoneBanner.Opacity, SubZoneText = Window.ZoneBanner.Text,
            EorzeaText = Config.IsGratis && Config.ShowEorzeaTime ? EorzeaTime.Format(Context.UnixTimeSeconds) : "",
        };
    }

    public void Save(string path) {
        SavedFrameObserver?.Invoke(this);
        var renderer = new SoftwareRenderer(Width, Height, Textures);
        var data = ImGui.GetDrawData();
        var previousOrigin = data.DisplayPos;
        data.DisplayPos = script.ScreenOrigin;
        try {
            renderer.Render(data, Context.Blurs);
        }
        finally { data.DisplayPos = previousOrigin; }
        renderer.Save(path);
    }

    internal Action? DrawPreviewOverlay { get; set; }
    internal bool PreserveWindowZOrder { get; set; }
    internal static Action<ScriptedFrameDriver>? SavedFrameObserver { get; set; }

    public void UpdateWindow() {
    }
    public Vector2 CoordinateOffset(Vector2 position) => -position * script.MapSizeFactor + script.MapOffset
        * script.MapSizeFactor;
    public MapContentBounds? ContentBounds => contentReady ? contentOverride ?? scannedContent : null;
    public bool SmallMapHint => script.SmallInstance || script.MapSizeFactor >= 2;
    public unsafe void DrawBackgroundTexture() {
        MapDrawList = (nint)ImGui.GetWindowDrawList().NativePtr;
        Renderer.DrawMapImage(map with { Size = textureSizeOverride ?? map.Size });
    }

    public void DrawFogOfWar() {
        if (redraws < 2)
            return;
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0);
        Renderer.DrawMapImage(map with { Size = textureSizeOverride ?? map.Size });
        ImGui.PopStyleVar();
    }

    public void CollectMarkers() {
        using var probe = BenchmarkProbe.Measure(BenchmarkSection.CollectMarkers);
        for (var index = 0; index < Markers.Count; index++) {
            var marker = Markers[index];
            var world = script.Markers[index].World;
            marker.Scale = Config.MapScale;
            marker.Position = (new Vector2(1024) - CoordinateOffset(world)) * Config.MapScale;
            marker.Offset = Renderer.DrawPosition;
            DrawHelpers.DrawMapMarker(marker);
        }
    }

    public void DrawFlag() {
    }

    public void DrawTextMarkers() {
        using var probe = BenchmarkProbe.Measure(BenchmarkSection.MapLabels);
        foreach (var (marker, text, world) in BenchmarkLabels) {
            marker.Scale = Config.MapScale;
            marker.Offset = Renderer.DrawPosition;
            marker.Position = (new Vector2(1024) - CoordinateOffset(world)) * Config.MapScale;
            var before = ImGui.GetWindowDrawList().IdxBuffer.Size;
            DrawHelpers.DrawText(marker, text);
            if (ImGui.GetWindowDrawList().IdxBuffer.Size > before) BenchmarkLabelsDrawn++;
        }
    }

    public void DrawPlayer() {
        var center = ImGui.GetWindowPos() + Renderer.DrawPosition + (new Vector2(1024) - CoordinateOffset(player))
            * Config.MapScale;
        var angle = Config.IsGratis
            ? PlayerOrientation.Advance(Config, MathF.PI / 2 - heading, cameraYaw, Now, FrameId, MapKey,
                new Vector3(player.X, 0, player.Y), nativeConeDegrees)
            : heading;
        PlayerDrawing.DrawLookLine(center, Config.MapScale, angle);
        PlayerDrawing.DrawPlayerIcon(center, Context.GetIcon(60443), angle, Config.MapScale);
    }

    public void DrawToolbar() {
        var cursor = ImGui.GetCursorPos();
        MapToolbarDrawing.Draw(activate, layers, Palette);
        if (RepeatToolbarForVerification) {
            ImGui.SetCursorPos(cursor);
            MapToolbarDrawing.Draw(activate, layers);
        }
    }

    public void DrawCoordinates(bool mapHovered, Vector2 offset) =>
        MapCoordinateDrawing.Draw(script.PlayerCoordinates ?? player, script.ShowPlayerCoordinates,
            mapHovered ? script.CursorCoordinates
                ?? (ImGui.GetMousePos() - offset - Renderer.DrawPosition) / Config.MapScale
                : null);
    public void DrawContextMenu(Vector2 offset) => ContextMenuCalls++;
    public void DrawSpoilerWarning() => ImGui.Text("Script requires AcceptedSpoilerWarning in its config.");
    private void Activate(ToolbarAction action) {
        switch (action) {
            case ToolbarAction.Search:
                if (Config.IsGratis && Config.Palette)
                    Palette?.Open();
                break;
            case ToolbarAction.Follow:
                Config.FollowPlayer = !Config.FollowPlayer;
                break;
            case ToolbarAction.Settings:
                SettingsOpen = !SettingsOpen;
                break;
            case ToolbarAction.CenterPlayer:
                Renderer.CenterOnCoordinate(player);
                break;
            case ToolbarAction.CenterMap:
                Config.FollowPlayer = false;
                Renderer.DrawOffset = Vector2.Zero;
                break;
            case ToolbarAction.Layers:
                ImGui.OpenPopup("Gratis_Show_Layers");
                break;
        }
    }

    private static void DrawLayers() {
        if (!ImGui.BeginPopup("Gratis_Show_Layers"))
            return;
        ImGui.Text("Script map layer");
        ImGui.EndPopup();
    }

    public void Dispose() {
        Context.Dispose();
        ImGuiErrors.DestroyContext(native);
        MapDrawContext.Current = previous;
    }
}
