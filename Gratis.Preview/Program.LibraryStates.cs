using System.Numerics;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class Program {
    private static bool TryLibraryStateScene(string[] args) {
        var at = Array.IndexOf(args, "--scene");
        if (at < 0 || at + 1 >= args.Length || !args[at + 1].StartsWith("library-", StringComparison.Ordinal))
            return false;
        var scene = args[at + 1];
        if (scene is not ("library-toolbar" or "library-toolbar-hover" or "library-bottom-cards"
            or "library-location" or "library-feedback" or "library-update" or "library-teleport"
            or "library-teleport-disabled" or "library-popup" or "library-popup-exit"
            or "library-location-exit" or "library-feedback-exit" or "library-filters"
            or "library-filters-quiet" or "library-filters-hidden" or "library-filters-solo" or "library-filters-loud"
            or "library-move" or "library-ghost"))
            throw new ArgumentException("Unknown library fixture: " + scene);
        string Value(string key, string fallback) {
            var index = Array.IndexOf(args, key);
            return index < 0 ? fallback : args[index + 1];
        }
        var size = Value("--size", "960x740").Split('x');
        var width = int.Parse(size[0]);
        var height = int.Parse(size[1]);
        var config = ChromeLayoutVerification.Config(new(width - 64, height - 64));
        config.VisualStyle = Value("--style", "gratis") == "classic" ? 1 : 0;
        config.ReduceMotion = false;
        config.InterfaceAnimations = InterfaceAnimationMode.Full;
        config.LocationAnchor = ChromeAnchor.TopLeft;
        config.PaletteHintSeen = true;
        config.TravelConfirmGil = 0;
        config.AlwaysShowToolbar = true;
        if (scene == "library-move") config.ClassicMoveHandle = config.MoveHandle = true;
        if (scene == "library-ghost") config.GhostMode = true;
        var state = Value("--state", "rest");
        using var driver = new ScriptedFrameDriver(config, new FrameScript {
            Width = width, Height = height, Palette = true,
            Title = "Eorzea - La Noscea - Limsa Lominsa Lower Decks - Hawkers' Alley###GratisMapWindow",
            PlayerCoordinates = new(18.4f, 23.7f), CursorCoordinates = new(22.6f, 19.2f),
            Markers = [new(60453, new(-80, 0)), new(60412, new(120, 80))],
        });
        driver.PreserveWindowZOrder = true;
        if (scene.StartsWith("library-location", StringComparison.Ordinal))
            driver.Context.LocationSource = new LocationMenuVerification.Source(driver);
        var ui = driver.Context.Assistance = new MapAssistance(new FakeTravelIpc().Provider(),
            new TravelFixture(driver), () => true, () => { });
        driver.Context.ContextMenu.Actions = new ContextMenuFixture();
        double time = 0;
        void Step(Vector2? mouse = null, bool left = false, bool right = false, bool escape = false,
            double dt = .04, ImGuiKey[]? keys = null) =>
            driver.Step(new(time += dt, Mouse: mouse ?? new(-1000), Left: left, Right: right,
                Escape: escape, Keys: keys));
        void Click(Vector2 point) { Step(point); Step(point, true); Step(point); }
        try {
            if (scene == "library-update") UpdateNotice.Available = "0.1.0.4";
            for (var frame = 0; frame < 16; frame++) Step();
            if (scene.StartsWith("library-location", StringComparison.Ordinal)) {
                Click(CenterBounds(LocationMenuDrawing.CardBounds));
                if (!LocationMenuDrawing.IsOpen) throw new InvalidOperationException("Location fixture did not open.");
            }
            if (scene.StartsWith("library-feedback", StringComparison.Ordinal)) {
                Click(CenterBounds(FeedbackButtonDrawing.LastBounds));
                if (!FeedbackButtonDrawing.PopupOpen) throw new InvalidOperationException("Feedback fixture did not open.");
            }
            if (scene.StartsWith("library-popup", StringComparison.Ordinal)) {
                var point = config.WindowPosition + config.WindowSize / 2;
                Step(point); Step(point, right: true); Step(point);
                if (!driver.Context.ContextMenu.IsOpen) throw new InvalidOperationException("Context fixture did not open.");
            }
            if (scene.StartsWith("library-teleport", StringComparison.Ordinal)) {
                ui.HoverPlace(TravelFixture.Hub, driver.Renderer.WorldToScreen(new(-80, 0), driver.Window.MapDrawOffset));
            }
            if (scene.StartsWith("library-filters", StringComparison.Ordinal)) {
                if (scene.EndsWith("quiet", StringComparison.Ordinal))
                    driver.Context.Visibility.Overrides[(int)MarkerGroup.Services] = MarkerVisibilityState.Quiet;
                if (scene.EndsWith("hidden", StringComparison.Ordinal))
                    driver.Context.Visibility.Overrides[(int)MarkerGroup.Services] = MarkerVisibilityState.Hidden;
                if (scene.EndsWith("loud", StringComparison.Ordinal))
                    driver.Context.Visibility.Overrides[(int)MarkerGroup.Services] = MarkerVisibilityState.Loud;
                if (scene.EndsWith("solo", StringComparison.Ordinal)) driver.Context.Visibility.Solo(MarkerGroup.Services);
                Click(CenterBounds(MapToolbarDrawing.ButtonBounds[2]));
            }
            for (var frame = 0; frame < 16; frame++)
                driver.Step(new(time += .04, Mouse: new(-1000)));
            if (scene == "library-teleport-disabled") {
                ui.Request(TravelFixture.Hub);
                Step(); Step();
            }
            if (scene.StartsWith("library-teleport", StringComparison.Ordinal) && ui.CardMax == Vector2.Zero)
                throw new InvalidOperationException($"{scene}: teleport fixture did not open.");
            if (scene.EndsWith("-exit", StringComparison.Ordinal)) {
                Step(escape: true);
                Step(dt: .04);
                if (!ReferenceEquals(PopupDiagnostics.Context, driver.Context) || PopupDiagnostics.LastFrame != driver.FrameId
                    || PopupDiagnostics.LastKind != "exit" || PopupDiagnostics.LastAlpha is <= 0 or >= 1)
                    throw new InvalidOperationException($"{scene}: expected a visible mid-exit popup; "
                        + $"kind={PopupDiagnostics.LastKind}, alpha={PopupDiagnostics.LastAlpha}, "
                        + $"frame={PopupDiagnostics.LastFrame}/{driver.FrameId}.");
            }
            var target = scene switch {
                "library-move" => driver.Window.MoveHandle.Geometry.Center,
                "library-update" => UpdateNotice.ActionCenter,
                "library-teleport" => ui.TravelButtonCenter,
                "library-toolbar-hover" or "library-toolbar" => config.IsGratis
                    ? CenterBounds(MapToolbarDrawing.ButtonBounds.First(b => b.Z > b.X))
                    : new(MapToolbarDrawing.LastBounds.X + 16, MapToolbarDrawing.LastBounds.Y + 16),
                _ when scene.StartsWith("library-filters", StringComparison.Ordinal) =>
                    CenterBounds(FilterBarDrawing.ChipBounds[(int)MarkerGroup.Services]),
                _ => new Vector2(-1000),
            };
            if (scene is "library-toolbar-hover" or "library-move" || state is "hover" or "pressed") {
                for (var frame = 0; frame < 6; frame++) Step(target);
                if (state == "pressed") { Step(target, true); Step(target, true); }
            }
            if (state == "focused") {
                var targetId = scene switch {
                    "library-teleport" => "##travel-action",
                    "library-update" => "##update-action",
                    _ when scene.StartsWith("library-filters", StringComparison.Ordinal) => "##filter-" + (int)MarkerGroup.Services,
                    _ => "",
                };
                FocusGalleryButton(driver, targetId, ref time);
            }
            var output = Path.GetFullPath(Value("--out", Path.Combine("_preview", scene + ".png")));
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            driver.Save(output);
            Console.WriteLine(output);
        }
        finally { if (scene == "library-update") UpdateNotice.Available = ""; }
        return true;
    }

    private static Vector2 CenterBounds(Vector4 bounds) => new((bounds.X + bounds.Z) / 2, (bounds.Y + bounds.W) / 2);

    private static void FocusGalleryButton(ScriptedFrameDriver driver, string id, ref double time) {
        if (id.Length == 0) throw new ArgumentException("No focus target supplied.");
        var previous = ChromeControls.PreviewBeforeButton;
        var previousAfter = ChromeControls.PreviewAfterButton;
        var previousZOrder = driver.PreserveWindowZOrder;
        var focused = false;
        var requestedFrame = -1;
        try {
            driver.PreserveWindowZOrder = true;
            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
            var navigationKey = driver.Palette?.IsOpen == true ? ImGuiKey.UpArrow : ImGuiKey.Tab;
            driver.Step(new(time += .04, Mouse: new(-1000), Keys: [navigationKey]));
            ChromeControls.PreviewBeforeButton = candidate => {
                if (candidate == id && requestedFrame != ImGui.GetFrameCount()) {
                    requestedFrame = ImGui.GetFrameCount();
                    ImGui.SetWindowFocus();
                    ImGui.SetKeyboardFocusHere();
                }
            };
            ChromeControls.PreviewAfterButton = candidate => {
                previousAfter?.Invoke(candidate);
                if (candidate == id) focused |= ImGui.IsItemFocused();
            };
            for (var i = 0; i < 6; i++) driver.Step(new(time += .04, Mouse: new(-1000)));
            if (!focused) throw new InvalidOperationException("Native focus did not reach " + id);
        }
        finally {
            driver.PreserveWindowZOrder = previousZOrder;
            ChromeControls.PreviewBeforeButton = previous;
            ChromeControls.PreviewAfterButton = previousAfter;
        }
    }
}
