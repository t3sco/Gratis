using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using Gratis.Classes;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class Program
{
    private static readonly string[] Scenes = ["map-default", "map-circular", "hover-tooltip", "markers", "toolbar", "map-classic", "map-glint", "map-glint-circular", "map-feather", "map-feather-circular", "map-edge-legacy", "map-edge-legacy-circular", "glass-toolbar", "glass-card", "glass-tinted", "glass-circle", "glass-unavailable", "chrome-hidden", "chrome-visible", "chrome-fading", "map-recolor-midnight", "map-recolor-mono", "map-recolor-blueprint", "map-recolor-sepia", "map-recolor-original", "map-recolor-custom", "map-recolor-sheet"];

    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try {
            if (args is ["--selftest", "viewport"]) {
                ViewportVerification.Run();
                return 0;
            }
            var options = Options.Parse(args);
            if (options is null) return 0;
            Run(options);
            return 0;
        }
        catch (Exception error) {
            Console.Error.WriteLine($"Preview failed: {error.Message}");
            return 1;
        }
    }

    private static unsafe void Run(Options options)
    {
        if (options.Scene == "map-recolor-sheet") { RenderRecolourSheet(options); return; }
        var native = ImGui.CreateContext();
        try {
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2(options.Width, options.Height);
            io.DisplayFramebufferScale = Vector2.One;
            io.DeltaTime = 1f / 60;
            io.NativePtr->IniFilename = null;
            io.NativePtr->LogFilename = null;
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
            ImGui.StyleColorsDark();
            var textures = new Textures();
            using var context = new PreviewContext(textures);
            MapDrawContext.Current = context;
            if (options.ViewportOff) context.Config.ScaleWithFrame = context.Config.ClampToMap = false;
            context.Config.MapCircle = options.Scene is "map-circular" or "map-circular-nonsquare" or "chrome-tiny-circle" or "map-glint-circular" or "map-feather-circular" or "map-edge-legacy-circular";
            context.Config.FeatherEnabled = !options.Scene.StartsWith("map-edge-legacy", StringComparison.Ordinal);
            if (options.FeatherWidth is { } featherWidth) context.Config.FeatherWidth = featherWidth;
            context.Config.VisualStyle = options.Scene == "map-classic" ? 1 : 0;
            var materialShowcase = options.Scene is "map-glint" or "map-glint-circular" or "glass-card" or "glass-tinted" or "glass-unavailable";
            context.BlurAvailable = options.Scene != "glass-unavailable";
            context.Config.GlassBlur = options.Scene != "glass-tinted";
            if (materialShowcase || options.Scene == "map-classic" || options.Scene.StartsWith("glass-", StringComparison.Ordinal) || options.Scene.StartsWith("chrome-", StringComparison.Ordinal)) {
                context.Config.GlassEnabled = true;
                context.Config.GlassGrain = true;
                context.Config.RingRotate = true;
            }
            if (context.Config.IsGratis) context.Config.ScaleWithZoom = false;
            context.BuildFonts(Path.Combine(AppContext.BaseDirectory, "assets", "NotoSans-Regular.ttf"));
            io.Fonts.GetTexDataAsRGBA32(out nint atlasPixels, out var atlasWidth, out var atlasHeight, out _);
            var atlas = new byte[checked(atlasWidth * atlasHeight * 4)];
            Marshal.Copy(atlasPixels, atlas, 0, atlas.Length);
            io.Fonts.SetTexID(textures.Add(atlasWidth, atlasHeight, atlas));
            var mapId = options.Map is null ? Terrain.Create(textures) : textures.Load(options.Map);
            mapId = PrepareRecolourMap(options.Scene, textures, mapId, options.Map is null);
            var map = textures.Get(mapId);
            var mapTexture = new DrawTexture(mapId, new Vector2(map.Width, map.Height));
            var mapSize = new Vector2(Math.Min(options.Width - 64, 960), Math.Min(options.Height - 64, 640));
            if (IsViewportScene(options.Scene)) mapSize = ViewportSceneSize(options.Scene, mapSize);
            if (options.Scene == "toolbar") mapSize.Y = Math.Min(mapSize.Y, 320);
            if (context.Config.MapCircle && options.Scene != "map-circular-nonsquare") mapSize = new Vector2(Math.Min(mapSize.X, mapSize.Y));
            if (options.Scene == "map-circular-nonsquare") mapSize = new Vector2(640, 360);
            if (options.Scene is "chrome-tiny" or "chrome-tiny-circle") mapSize = new Vector2(320, 240);
            var mapPosition = (io.DisplaySize - mapSize) / 2;
            var mouse = options.Scene == "hover-tooltip" || materialShowcase
                ? mapPosition + mapSize * new Vector2(.56f, .53f)
                : options.Scene is "toolbar" or "glass-toolbar" ? mapPosition + new Vector2(194, 26) : new Vector2(-1000);
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
            Console.WriteLine($"Scene {options.Scene}; native Dear ImGui {ImGui.GetVersion()}; Geist / Noto atlas {atlasWidth}x{atlasHeight}.");
            Console.WriteLine($"Edge mask: {context.Config.FeatherShape}, {context.Config.FeatherWidth}px; map mesh {FeatheredMapDrawing.LastVertexCount} vertices (0 = legacy edge).");
            Console.WriteLine($"Glass: {context.Blurs.Count} CPU box-blur approximations (not Dalamud GPU parity).");
            if (options.Verify) {
                MapColourVerification.Run();
                Verification.Run(context, textures);
                ChromeVerification.Run(context);
            }
        }
        finally { ImGui.DestroyContext(native); }
    }

    private static void DrawScene(string scene, Vector2 position, Vector2 size, DrawTexture map, bool viewportOff)
    {
        ImGui.SetNextWindowPos(position);
        ImGui.SetNextWindowSize(size);
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground |
                                      ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings |
                                      ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0);
        ImGui.Begin("Gratis preview", flags);
        ImGui.PopStyleVar(3);
        ImGui.BeginChild("render_child", size, ImGuiChildFlags.None, flags);
        MapShape.Update(ImGui.GetWindowSize());
        var context = MapDrawContext.Current;
        context.Chrome.Update(context.Config, ImGui.GetTime(), false,
            scene == "chrome-hidden" ? 0 : scene == "chrome-fading" ? .5f : 1);
        if (!DrawViewportMap(scene, size, map, viewportOff)) {
            var scale = Math.Max(size.X / map.Size.X, size.Y / map.Size.Y) * 1.15f;
            ImGui.SetCursorPos((size - map.Size * scale) / 2);
            MapTextureDrawing.DrawMapImage(map, scale);
        }
        var player = ImGui.GetWindowPos() + size * new Vector2(.45f, .63f);
        PlayerDrawing.DrawLookLine(player, .8f, -1.25f);
        PlayerDrawing.DrawPlayerIcon(player, MapDrawContext.Current.GetIcon(60443), .25f, 1);
        if (scene == "markers") {
            for (var row = 0; row < 4; row++)
                for (var col = 0; col < 6; col++)
                    Marker(size * new Vector2(.09f + col * .16f, .20f + row * .20f), size,
                        (uint)(60010 + row * 6 + col), $"Marker {row * 6 + col + 1}", row == 2 ? 42 : null,
                        category: (MarkerCategory)(col + 1));
        }
        else {
            Marker(size * new Vector2(.27f, .36f), size, scene == "map-classic" ? 60401u : 60453u, "Aetheryte", null);
            Marker(size * new Vector2(.74f, .70f), size, 60071, "Survey objective", 48);
            Marker(new Vector2(5), size, 60012, "Clipping probe", null);
        }
        Marker(size * new Vector2(.56f, .53f), size, 60071, "The Wanderer's Camp", null,
            "Level 50 - Preview marker");
        if (scene.StartsWith("map-recolor-", StringComparison.Ordinal)) DrawRecolourMarkers(size);
        if (scene == "glass-circle") {
            var center = ImGui.GetWindowPos() + size / 2;
            MapWindowDrawing.DrawSurface(ImGui.GetWindowDrawList(), center - new Vector2(40), center + new Vector2(40), 40);
        }
        ImGui.SetCursorPos(Vector2.Zero);
        MapToolbarDrawing.Draw(_ => { }, DrawFakeLayers);
        MapWindowDrawing.DrawFrameEffects();
        if (MapDrawContext.Current.Config.IsGratis) MapCoordinateDrawing.Draw(new Vector2(18.4f, 23.7f), true,
            scene is "hover-tooltip" or "map-glint" or "map-glint-circular" or "glass-card" or "glass-tinted" or "glass-unavailable" ? new Vector2(22.6f, 19.2f) : null);
        MapWindowDrawing.DrawTitleOnMap("Eorzea - La Noscea - Middle La Noscea###GratisMapWindow");
        ImGui.EndChild();
        ImGui.End();
    }

    private static void Marker(Vector2 position, Vector2 mapSize, uint icon, string text, float? radius, string? secondary = null, MarkerCategory category = MarkerCategory.Auto)
    {
        DrawHelpers.DrawMapMarker(new MarkerInfo {
            Position = position, Offset = Vector2.Zero, Scale = Math.Max(mapSize.X, mapSize.Y) / 2048,
            IconId = icon, PrimaryText = () => text, SecondaryText = () => secondary, Radius = radius, Category = category,
        });
    }

    private static void DrawFakeLayers()
    {
        if (!ImGui.BeginPopup("Gratis_Show_Layers")) return;
        ImGui.MenuItem("Preview surface", "", true);
        ImGui.MenuItem("Preview underground");
        ImGui.EndPopup();
    }

    private sealed record Options(string Scene, string Output, int Width, int Height, string? Map, float? FeatherWidth, bool Verify, bool ViewportOff)
    {
        public static Options? Parse(string[] args)
        {
            var scene = "map-default";
            string? output = null, map = null;
            float? featherWidth = null;
            var verify = false;
            var viewportOff = false;
            var width = 1280; var height = 800;
            for (var i = 0; i < args.Length; i++) {
                if (args[i] == "--verify") { verify = true; continue; }
                if (args[i] == "--viewport-off") { viewportOff = true; continue; }
                if (args[i] is "--help" or "-h") {
                    Console.WriteLine($"dotnet run --project Gratis.Preview -- --scene <{string.Join('|', Scenes.Concat(ViewportScenes))}> [--out path.png] [--size WxH] [--map image.png] [--feather-width 0..48] [--verify] [--viewport-off]; --selftest viewport");
                    return null;
                }
                if (i + 1 >= args.Length) throw new ArgumentException($"Missing value for {args[i]}.");
                var key = args[i++]; var value = args[i];
                switch (key) {
                    case "--scene": scene = value; break;
                    case "--out": output = value; break;
                    case "--map": map = value; break;
                    case "--feather-width":
                        if (!float.TryParse(value, out var feather) || !float.IsFinite(feather) || feather < 0 || feather > 48)
                            throw new ArgumentException("Feather width must be 0..48.");
                        featherWidth = feather;
                        break;
                    case "--size": {
                        var parts = value.Split('x', 'X');
                        if (parts.Length != 2 || !int.TryParse(parts[0], out width) || !int.TryParse(parts[1], out height))
                            throw new ArgumentException("Size must be WxH, for example 1280x800.");
                        break;
                    }
                    default: throw new ArgumentException($"Unknown option {key}.");
                }
            }
            if (!Scenes.Contains(scene) && !IsViewportScene(scene) && !FrameScenes.Contains(scene)) throw new ArgumentException($"Unknown scene '{scene}'. Expected {string.Join(", ", Scenes.Concat(ViewportScenes).Concat(FrameScenes))}.");
            if (width < 320 || height < 240 || width > 8192 || height > 8192 || (long)width * height > 16777216)
                throw new ArgumentException("Size must be 320x240 to 8192x8192, with at most 16 megapixels.");
            if (map is not null && (!File.Exists(map) || !Path.GetExtension(map).Equals(".png", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("--map must name an existing PNG.");
            if (output is not null && !Path.GetExtension(output).Equals(".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("--out must have a .png extension.");
            output ??= Path.Combine(ProjectRoot(), "_preview", scene + ".png");
            return new Options(scene, output, width, height, map, featherWidth, verify, viewportOff);
        }

        private static string ProjectRoot()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Gratis.sln"))) return dir.FullName;
            return Directory.GetCurrentDirectory();
        }
    }
}
