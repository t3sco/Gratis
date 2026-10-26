using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class Program {
    private static readonly string[] Scenes = [
        "spotlight",
        "spotlight-circular",
        "settings-window",
        "settings-filters",
        "settings-appearance",
        "settings-classic",
        "settings-search",
        "settings-map",
        "settings-markers",
        "settings-chrome",
        "settings-quest-journal",
        "settings-tools",
        "settings-advanced",
        "map-default",
        "map-circular",
        "hover-tooltip",
        "markers",
        "toolbar",
        "map-classic",
        "map-glint",
        "map-glint-circular",
        "map-feather",
        "map-feather-circular",
        "map-edge-legacy",
        "map-edge-legacy-circular",
        "glass-on",
        "glass-off",
        "glass-toolbar",
        "glass-card",
        "glass-tinted",
        "glass-circle",
        "glass-unavailable",
        "chrome-hidden",
        "chrome-visible",
        "chrome-fading",
        "map-recolor-midnight",
        "map-recolor-mono",
        "map-recolor-blueprint",
        "map-recolor-sepia",
        "map-recolor-original",
        "map-recolor-custom",
        "map-recolor-sheet"];

    public static int Main(string[] args) {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try {
            if (args is ["--selftest", var test])
                return RunSelftests(test);
            if (TrySpecialScene(args)) {
                ImGuiErrors.ThrowIfNewErrors(0);
                return 0;
            }
            var options = Options.Parse(args);
            if (options is null)
                return 0;
            Run(options);
            ImGuiErrors.ThrowIfNewErrors(0);
            return 0;
        }
        catch (Exception error) {
            Console.Error.WriteLine($"Preview failed: {error.Message}");
            return 1;
        }
    }

    private static bool TrySpecialScene(string[] args) => TryViewportCoverScene(args) || TryAutoFitScene(args)
        || TryWindowPresetsScene(args)
        || TryArrowSmoothingScene(args) || TryCameraScene(args) || TrySmallInstanceScene(args)
        || TryChromeLayoutScene(args) || TryMeasureScene(args) || TryNearestScene(args) || TryEdgePointerScene(args)
        || TryDiff(args) || TryLiveScene(args) || TryFrameScript(args) || TryMoveHandleScene(args)
        || TryPaletteScene(args) || TryTravelScene(args) || TryAetheryteScene(args) || TryPinsScene(args)
        || TryContextMenuScene(args) || TryFlagArrivalScene(args) || TryGhostScene(args) || TryPlaceChipScene(args)
        || TryAttunementScene(args) || TryQuestLogScene(args);

    private static unsafe void Run(Options options, bool chromeSelftest = false) {
        if (IsLensScene(options.Scene)) {
            RenderLensScene(options);
            return;
        }
        if (options.Scene.StartsWith("settings-", StringComparison.Ordinal)
            || options.Scene.StartsWith("tweaks-", StringComparison.Ordinal)) {
            RenderSettings(options);
            return;
        }
        if (options.Scene is "spotlight" or "spotlight-circular") {
            RenderSpotlightSheet(options);
            return;
        }
        if (options.Scene == "player-heading") {
            RenderHeadingSheet(options);
            return;
        }
        if (options.Scene == "map-recolor-sheet") {
            RenderRecolourSheet(options);
            return;
        }
        if (options.Scene == "frame-presets") {
            RenderFramePresets(options);
            return;
        }
        if (options.Scene == "follow-return") {
            RenderFollowReturnSheet(options);
            return;
        }
        var native = ImGuiErrors.CreateContext();
        try {
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2(options.Width, options.Height);
            io.DisplayFramebufferScale = Vector2.One;
            io.DeltaTime = 1f / 60;
            if (IsMotionScene(options.Scene))
                io.DeltaTime = .025f;
            io.NativePtr->IniFilename = null;
            io.NativePtr->LogFilename = null;
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
            ImGui.StyleColorsDark();
            var textures = new Textures();
            using var context = new PreviewContext(textures);
            MapDrawContext.Current = context;
            if (options.Scene == "players")
                context.DpiScale = options.Dpi;
            if (options.Scene is "markers-legend" or "markers-quests") {
                context.DpiScale = options.Dpi;
                context.Config.Markers.Declutter = false;
                context.Config.Markers.IconStyle = options.IconStyle;
                context.Config.MapRounding = 0;
            }
            ConfigureMarkerPreview(options.Scene, context, options.Declutter);
            if (options.ViewportOff)
                context.Config.ScaleWithFrame = context.Config.ClampToMap = false;
            context.Config.MapCircle =
                options.Scene is "map-circular" or "map-circular-nonsquare" or "chrome-tiny-circle"
                    or "map-glint-circular" or "map-feather-circular" or "map-edge-legacy-circular";
            context.Config.FeatherEnabled = !options.Scene.StartsWith("map-edge-legacy", StringComparison.Ordinal);
            if (options.FeatherWidth is { } featherWidth)
                context.Config.FeatherWidth = featherWidth;
            context.Config.VisualStyle = options.Scene == "map-classic" ? 1 : 0;
            if (options.Scene == "motion-reduced")
                context.Config.ReduceMotion = true;
            if (options.Frame is { } framePreset)
                context.Config.FramePreset = framePreset;
            if (options.Circle is { } circle)
                context.Config.MapCircle = circle;
            var materialShowcase = options.Scene is "map-glint" or "map-glint-circular" or "glass-on" or "glass-off"
                or "glass-card" or "glass-tinted" or "glass-unavailable";
            context.BlurAvailable = options.Scene != "glass-unavailable";
            context.Config.GlassBlur = options.Scene != "glass-tinted";
            if (materialShowcase || options.Scene == "map-classic"
                || options.Scene.StartsWith("glass-", StringComparison.Ordinal)
                || options.Scene.StartsWith("chrome-", StringComparison.Ordinal)) {
                context.Config.GlassEnabled = true;
                context.Config.GlassGrain = true;
                context.Config.RingRotate = true;
            }
            if (options.Scene == "glass-off")
                context.Config.GlassEnabled = false;
            if (options.Scene is "glass-on" or "glass-off")
                context.Config.GlassGrain = false;
            if (context.Config.IsGratis)
                context.Config.ScaleWithZoom = false;
            context.BuildFonts(Path.Combine(AppContext.BaseDirectory, "assets", "NotoSans-Regular.ttf"));
            io.Fonts.GetTexDataAsRGBA32(out nint atlasPixels, out var atlasWidth, out var atlasHeight, out _);
            var atlas = new byte[checked(atlasWidth * atlasHeight * 4)];
            Marshal.Copy(atlasPixels, atlas, 0, atlas.Length);
            io.Fonts.SetTexID(textures.Add(atlasWidth, atlasHeight, atlas));
            if (chromeSelftest) {
                ChromeVerification.Run(context, textures);
                return;
            }
            var mapId = options.Map is null ? Terrain.Create(textures) : textures.Load(options.Map);
            mapId = PrepareRecolourMap(options.Scene, textures, mapId, options.Map is null);
            var map = textures.Get(mapId);
            var mapTexture = new DrawTexture(mapId, new Vector2(map.Width, map.Height));
            var mapSize = new Vector2(Math.Min(options.Width - 64, 960), Math.Min(options.Height - 64, 640));
            if (options.Scene is "markers-legend" or "markers-quests")
                mapSize = new Vector2(Math.Min(options.Width - 64 * options.Dpi, 960 * options.Dpi),
                    Math.Min(options.Height - 64 * options.Dpi, 640 * options.Dpi));
            if (IsViewportScene(options.Scene))
                mapSize = ViewportSceneSize(options.Scene, mapSize);
            if (options.Scene == "toolbar")
                mapSize.Y = Math.Min(mapSize.Y, 320);
            if (context.Config.MapCircle && options.Scene != "map-circular-nonsquare")
                mapSize = new Vector2(Math.Min(mapSize.X, mapSize.Y));
            if (options.Scene == "map-circular-nonsquare")
                mapSize = new Vector2(640, 360);
            if (options.Scene is "chrome-tiny" or "chrome-tiny-circle")
                mapSize = new Vector2(320, 240);
            var mapPosition = (io.DisplaySize - mapSize) / 2;
            var mouse = options.Scene == "markers-enemies" ? mapPosition + mapSize * new Vector2(.23f, .35f)
                : options.Scene == "markers-clustered" ? mapPosition + mapSize / 2
                : options.Scene == "hover-tooltip" || materialShowcase ? mapPosition + mapSize * new Vector2(.56f, .53f)
                : options.Scene is "toolbar" or "glass-toolbar" ? mapPosition + new Vector2(194, 26)
                : new Vector2(-1000);
            for (var frame = 0; frame < 12; frame++) {
                context.Blurs.Clear();
                io.AddMousePosEvent(mouse.X, mouse.Y);
                ImGui.NewFrame();
                DrawScene(options.Scene, mapPosition, mapSize, mapTexture, options.ViewportOff);
                ImGui.Render();
            }
            var renderer = new SoftwareRenderer(options.Width, options.Height, textures);
            renderer.Render(ImGui.GetDrawData(), context.Blurs);
            renderer.Save(options.Output);
            Console.WriteLine($"Scene {options.Scene}; native Dear ImGui {ImGui.GetVersion()}; Geist / Noto "
                + $"atlas {atlasWidth}x{atlasHeight}.");
            Console.WriteLine($"Edge mask: {context.Config.FeatherShape}, {context.Config.FeatherWidth}px; map "
                + $"mesh {FeatheredMapDrawing.LastVertexCount} vertices (0 = legacy edge).");
            Console.WriteLine($"Glass: {context.Blurs.Count} CPU box-blur approximations (not Dalamud GPU parity).");
            Console.WriteLine(context.Blur.Status(context.Config));
            if (IsMarkerScene(options.Scene))
                VerifyMarkers(context, options.Scene);
            if (IsMotionScene(options.Scene))
                ReportMotion(options.Scene, context);
            if (options.Verify) {
                MapColourVerification.Run();
                Verification.Run(context, textures);
                ChromeVerification.Run(context, textures);
            }
        }
        finally { ImGuiErrors.DestroyContext(native); }
    }


}
