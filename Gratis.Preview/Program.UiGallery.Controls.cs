using System.Numerics;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;
using ImGuiNET;

namespace Gratis.Preview;

internal static partial class Program {
    private static readonly string[] GalleryStates = ["rest", "hover", "pressed", "focused", "selected", "disabled"];
    private sealed record GalleryCell(string Label, string? Path = null, Vector4 Bounds = default);
    private sealed record GalleryRow(string Label, string Source, GalleryCell[] Cells, int Height = 118);
    private sealed record GalleryPrimitive(string Kind, string Label, string Source, string States, bool GratisOnly = false);

    private static GalleryPrimitive[] GalleryPrimitives() => [
        new("button", "Button substrate", "ChromeControls.Button + Feedback", "rhpfd"),
        new("icon", "Icon / toolbar glyph", "ChromeControls.IconButton", "rhpfd", true),
        new("close", "Close button", "ChromeControls.IconButton: X", "rhpfd", true),
        new("pin", "Pin button", "ChromeControls.IconButton: Pin", "rhpfd", true),
        new("search", "Search button", "ChromeControls.IconButton: Search", "rhpfd", true),
        new("text", "Text / native action", "ChromeControls.TextButton", "rhpfd"),
        new("quiet", "Quiet action", "MapAssistance.QuietButton", "rhpfd", true),
        new("action", "Icon action", "MapAssistance.ActionButton", "rhpfd", true),
        new("primary", "Primary / Send", "MapAssistance.PrimaryTravelButton", "rhpfd", true),
        new("toggle", "Toggle / checkbox", "SettingsTheme.Toggle / native Classic", "rhpfsd"),
        new("slider", "Scalar slider", "SettingsTheme.Slider / native Classic", "rhpfd"),
        new("combo", "Combo / dropdown", "ChromeControls.Combo", "rhpfd"),
        new("menu", "Menu row", "ChromeControls.MenuItem", "rhpfsd"),
        new("selectable", "Selectable row", "ChromeControls.Selectable", "rhpfsd"),
        new("pill", "Chip / pill", "PaletteRowDrawing.Pill", "r", true),
        new("row", "Row background", "PaletteRowDrawing.Background", "rhs", true),
        new("field", "Text editor", "Native InputText (shared settings contract)", "rhpfd"),
        new("rgb", "Colour editor RGB", "Native ColorEdit3 (SettingsEntry contract)", "rhpfd"),
        new("rgba", "Colour editor RGBA", "Native ColorEdit4 (SettingsEntry contract)", "rhpfd"),
        new("binding", "Key binding capture", "LensDrawing.DrawKeyBinding", "rhpfd", true),
        new("surface", "Card / surface painter", "MapWindowDrawing.DrawSurface", "r", true),
        new("scroll", "Scroll region / bar", "UiMotion.Child + Scroll; native scrollbar", "rhp"),
        new("fonts", "Text roles / icon + label", "TextDrawing + GratisIcons", "r"),
    ];

    private static IEnumerable<GalleryRow> CaptureGalleryControls(bool classic, string directory) {
        Directory.CreateDirectory(directory);
        var config = new SystemConfig { AcceptedSpoilerWarning = true, ChromeIdleDelay = 2.1f, VisualStyle = classic ? 1 : 0,
            WindowSize = new(280, 200), InterfaceAnimations = InterfaceAnimationMode.Full };
        using var driver = new ScriptedFrameDriver(config, new FrameScript { Width = 320, Height = 240 });
        driver.BenchmarkMapVisible = false;
        double time = 0;
        foreach (var primitive in GalleryPrimitives()) {
            var cells = new List<GalleryCell>();
            foreach (var state in GalleryStates) {
                var code = state switch { "rest" => 'r', "hover" => 'h', "pressed" => 'p', "focused" => 'f',
                    "selected" => 's', _ => 'd' };
                if (classic && primitive.GratisOnly || !primitive.States.Contains(code)) {
                    cells.Add(new(classic && primitive.GratisOnly ? "Gratis only" : "No such state"));
                    continue;
                }
                var sampleId = primitive.Kind + "-" + state;
                if (state == "focused") ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
                else ImGui.GetIO().ConfigFlags &= ~ImGuiConfigFlags.NavEnableKeyboard;
                var focus = false;
                var observedHover = false;
                var observedActive = false;
                var observedFocus = false;
                driver.DrawPreviewOverlay = () => {
                    var theme = SettingsTheme.Push(config);
                    ImGui.SetNextWindowPos(Vector2.Zero);
                    ImGui.SetNextWindowSize(new(320, 240));
                    ImGui.SetNextWindowFocus();
                    ImGui.Begin("Gallery sample##" + sampleId, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove
                        | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar);
                    UiMotion.Begin();
                    ImGui.SetCursorScreenPos(new(24, 32));
                    ImGui.PushID(sampleId);
                    ImGui.BeginDisabled(state == "disabled");
                    if (focus) ImGui.SetKeyboardFocusHere();
                    DrawGalleryControl(primitive.Kind, state, config);
                    observedHover = ImGui.IsItemHovered();
                    observedActive = ImGui.IsItemActive();
                    observedFocus = ImGui.IsItemFocused();
                    ImGui.Dummy(Vector2.Zero);
                    ImGui.EndDisabled();
                    ImGui.PopID();
                    ImGui.End();
                    theme.Pop();
                };
                for (var frame = 0; frame < 12; frame++) {
                    focus = state == "focused" && frame == 3;
                    var mouse = state is "hover" or "pressed" ? new Vector2(40, 45) : new Vector2(-1000);
                    if (primitive.Kind == "scroll") mouse = new(211, 40);
                    driver.Step(new(time += .04, Mouse: mouse, Left: state == "pressed" && frame >= 8,
                        Keys: state == "focused" && frame == 2 ? [ImGuiKey.Tab] : [], Escape: frame == 0), false);
                }
                if (primitive.Kind is "button" or "icon" or "close" or "pin" or "search" or "quiet"
                    or "action" or "primary" or "text" or "toggle") {
                    if (state == "rest" && observedFocus && ImGui.GetIO().NavVisible
                        || state == "hover" && !observedHover || state == "pressed" && !observedActive
                        || state == "focused" && !observedFocus)
                        throw new InvalidOperationException($"Gallery did not reach {sampleId} (hover={observedHover}, "
                            + $"active={observedActive}, focus={observedFocus}).");
                }
                var path = Path.Combine(directory, sampleId + ".png");
                driver.Save(path);
                cells.Add(new(state, path, new(8, 16, 304, 112)));
            }
            yield return new(primitive.Label, primitive.Source, cells.ToArray());
        }
        driver.DrawPreviewOverlay = null;
    }

    private static void DrawGalleryControl(string kind, string state, SystemConfig config) {
        var selected = state == "selected";
        var min = ImGui.GetCursorScreenPos();
        var list = ImGui.GetWindowDrawList();
        switch (kind) {
            case "button":
                ChromeControls.Button("Button", new(132, 36));
                ChromeControls.Feedback(list, min, min + new Vector2(132, 36));
                break;
            case "icon": case "close": case "pin": case "search":
                ChromeControls.IconButton("##icon", kind switch { "close" => GratisIcons.X,
                    "pin" => GratisIcons.Pin, "search" => GratisIcons.Search, _ => GratisIcons.Settings },
                    "", new(36));
                break;
            case "text": ChromeControls.TextButton("Action", new(132, 36)); break;
            case "quiet": ActionButton.Draw("Cancel", "Cancel", new(ActionFaces.Quiet)); break;
            case "action": ActionButton.Draw("Show on map", "Show on map", new(ActionFaces.Action, Icon: GratisIcons.MapPin)); break;
            case "primary": ActionButton.Draw("Send", "Send", new(ActionFaces.PrimaryTravel, Icon: GratisIcons.Check)); break;
            case "toggle":
                if (config.IsGratis) SettingsTheme.Toggle("##toggle", ref selected);
                else ImGui.Checkbox("Enabled", ref selected);
                break;
            case "slider":
                var scalar = .55f;
                ImGui.SetNextItemWidth(240);
                if (config.IsGratis) SettingsTheme.Slider("##slider", ref scalar, 0, 1, "%.2f", config.AccentColor);
                else ImGui.SliderFloat("##slider", ref scalar, 0, 1, "%.2f");
                break;
            case "combo":
                var choice = 0;
                ImGui.SetNextItemWidth(200);
                ChromeControls.Combo("##combo", ref choice, ["Explore", "Quests", "Minimal"]);
                break;
            case "menu": ChromeControls.MenuItem("Show on map", "M", selected); break;
            case "selectable": ChromeControls.Selectable("Destination", selected, new(230, 36)); break;
            case "pill": PaletteRowDrawing.Pill(list, min, "Dawntrail", GratisTheme.WarmAccent, 1); break;
            case "row": PaletteRowDrawing.Background(list, min, new(240, 52), config.AccentColor,
                selected, state == "hover", 1); break;
            case "field":
                var query = "";
                ImGui.SetNextItemWidth(240);
                ImGui.InputTextWithHint("##field", "Search...", ref query, 256);
                break;
            case "rgb":
                var rgb = new Vector3(.4f, .74f, .74f);
                ImGui.SetNextItemWidth(240);
                ImGui.ColorEdit3("##rgb", ref rgb, ImGuiColorEditFlags.NoInputs);
                break;
            case "rgba":
                var rgba = config.AccentColor;
                ImGui.SetNextItemWidth(240);
                ImGui.ColorEdit4("##rgba", ref rgba, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar);
                break;
            case "binding": LensDrawing.DrawKeyBinding(config); break;
            case "surface": MapWindowDrawing.DrawSurface(list, min, min + new Vector2(240, 64), GratisTheme.Radius.Card); break;
            case "scroll":
                UiMotion.Child("scroll", new(200, 72), ImGuiWindowFlags.AlwaysVerticalScrollbar);
                for (var i = 0; i < 12; i++) ImGui.TextUnformatted("Scrollable item " + (i + 1));
                UiMotion.Scroll();
                ImGui.EndChild();
                break;
            case "fonts":
                var y = min.Y;
                foreach (var role in Enum.GetValues<ChromeFontRole>()) {
                    using var font = MapDrawContext.Current.GetChromeFont(14, role)?.Push();
                    TextDrawing.Draw(list, new(min.X + 24, y), ImGui.GetColorU32(GratisTheme.Primary), role.ToString());
                    y += 18;
                }
                GratisIcons.Draw(list, min + new Vector2(8, 9), GratisIcons.MapPin, GratisTheme.Secondary, 18);
                break;
        }
    }
}
