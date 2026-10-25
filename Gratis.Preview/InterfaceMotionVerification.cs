using System.Numerics;
using System.Text.Json;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;

namespace Gratis.Preview;

internal static class InterfaceMotionVerification
{
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Interface motion: " + message); }
    public static void Maths()
    {
        float At(float step, double end, InterfaceCurve curve, float initial, float target, float duration) {
            var s = new InterfaceMotionStore(); s.Begin(0, 0); s.Value(new(1), target, duration, curve, initial: initial);
            var time = 0d; var frame = 0; float value = initial;
            while (time < end) { time = Math.Min(end, time + step); s.Begin(time, ++frame); value = s.Value(new(1), target, duration, curve); }
            return value;
        }
        foreach (var step in new[] { .016f, .033f, .008f }) {
            Check(MathF.Abs(At(step, .09, InterfaceCurve.Enter, 0, 1, .18f) - .875f) < .0001f, "Enter follows the same absolute-time curve at 8/16/33ms");
            Check(At(step, .181, InterfaceCurve.Enter, 0, 1, .18f) == 1 && At(step, .101, InterfaceCurve.Exit, 1, 0, .10f) == 0, "Enter/exit settle at their token deadlines");
            var spring = At(step, .3, InterfaceCurve.Spring, 0, 100, .18f);
            Check(MathF.Abs(spring - At(.016f, .3, InterfaceCurve.Spring, 0, 100, .18f)) < .001f, "Selection spring is frame-rate independent");
        }
        var store = new InterfaceMotionStore();
        for (var i = 0; i < 1000; i++) {
            store.Begin(i * .4, i * 2); store.Value(new((uint)i), 1, .18f, initial: 0);
            store.Begin(i * .4 + .2, i * 2 + 1); store.Value(new((uint)i), 0, .1f);
            Check(store.Count <= InterfaceMotionStore.Capacity, "Opening/closing never exceeds the fixed key budget");
        }
        for (var i = 0; i < 256; i++) store.Begin(500 + i * .016, 3000 + i);
        Check(store.Count == 0, "Expired keys return their slots after 1000 opens/closes");
        store.Begin(600, 4000); store.Value(new(7), 1, .1f);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++) { store.Begin(601 + i * .016, 4001 + i); store.Value(new(7), 1, .1f); }
        Check(GC.GetAllocatedBytesForCurrentThread() - allocated < 128, "Steady-state motion sampling allocates no managed objects");
        store.Reset();
        for (var i = 0; i < InterfaceMotionStore.Capacity * 2; i++) {
            store.Begin(0, i); store.Value(new((uint)i), 1, .18f, initial: 0);
        }
        Check(store.Count == InterfaceMotionStore.Capacity, "A simultaneous key burst evicts reusable slots without growing storage");
        Console.WriteLine("Interface motion: enter/exit deadlines and spring parity at 8/16/33ms, 1000 lifecycles, expiry, bounded keys and allocation-free idle sampling.");
    }
    public static void Scroll()
    {
        foreach (var dt in new[] { .008f, .016f, .033f }) {
            var state = new InterfaceScroll(); state.Sync(0);
            state.Advance(0, 1000, 400, false, 0, false);
            for (var t = 0f; t < 1; t += dt) state.Advance(state.Position, 1000, 0, false, dt, false);
            Check(MathF.Abs(state.Position - 400) < .1f && MathF.Abs(state.Velocity) < .1f, "Wheel easing converges with an inertial tail");
            state.Advance(180, 1000, 0, true, dt, false);
            Check(state.Position == 180 && state.Target == 180 && state.Velocity == 0, "Scrollbar drag takes control immediately");
            state.Reveal(700, 760, 300); Check(state.Target == 460, "Keyboard navigation scrolls only enough to reveal the selected row");
            state.Advance(180, 1000, 0, false, dt, true); Check(state.Position == 460, "Reduced/Off scroll synchronises immediately");
        }
        Console.WriteLine("Interface scroll: convergence, scrollbar takeover, keyboard reveal and instant policy at three frame rates.");
    }
    public static void Config()
    {
        var c = JsonSerializer.Deserialize<SystemConfig>("{}", ScriptedFrameDriver.Json)!;
        Check(c.InterfaceAnimations == InterfaceAnimationMode.Full && c.AnimationSpeed == 1, "Old configs inherit Full at 1x");
        c.InterfaceAnimations = InterfaceAnimationMode.Reduced;
        Check(c.EffectiveInterfaceMotion == InterfaceAnimationMode.Reduced, "Reduced policy");
        c.InterfaceAnimations = InterfaceAnimationMode.Full; c.ReduceMotion = true;
        Check(c.EffectiveInterfaceMotion == InterfaceAnimationMode.Reduced, "Existing Reduce motion overrides Full");
        c.InterfaceAnimations = InterfaceAnimationMode.Off;
        Check(c.EffectiveInterfaceMotion == InterfaceAnimationMode.Off, "Off wins over Reduce motion");
        foreach (var path in new[] { "InterfaceAnimations", "AnimationSpeed" }) SettingsLayout.Entries.Single(e => e.Path == path).Reset(c, new());
        Check(c.InterfaceAnimations == InterfaceAnimationMode.Full && c.AnimationSpeed == 1, "Settings reset both options");
        var tweak = TweakCatalog.All.Single(t => t.Id == "interface-motion"); tweak.Set(c, false);
        Check(c.InterfaceAnimations == InterfaceAnimationMode.Off, "Tweak Off maps to the shared policy");
        tweak.Reset(c); Check(c.InterfaceAnimations == InterfaceAnimationMode.Full, "Tweak reset restores Full");
        c.VisualStyle = 1; Check(c.EffectiveInterfaceMotion == InterfaceAnimationMode.Off, "Classic opts out without changing its saved settings");
        Console.WriteLine("Interface settings: legacy defaults, Full/Reduced/Off precedence, speed/reset and Tweaks routing.");
    }
    public static void Frames()
    {
        var c = ChromeLayoutVerification.Config(new(780, 600)); c.ReduceMotion = false;
        using var d = new ScriptedFrameDriver(c, new FrameScript { Width = 1000, Height = 800, Palette = true });
        var travel = d.Context.Assistance = new MapAssistance(NullTravelProvider.Instance, new TravelFixture(d), () => true, () => { });
        d.Step(new(0)); d.Step(new(.2)); travel.HoverPlace(TravelFixture.Hub, new(330, 280));
        d.Step(new(.22)); d.Step(new(.44));
        var formerCard = (travel.CardMin + travel.CardMax) / 2;
        d.Step(new(.46, Escape: true)); d.Step(new(.47, Mouse: formerCard));
        Check(travel.CardMax == Vector2.Zero && !travel.InputActive, "Travel exit releases its hit region immediately");
        var before = d.Renderer.DrawOffset;
        d.Step(new(.48, Left: true)); var drag = d.Step(new(.50, Mouse: formerCard + new Vector2(35, 25)));
        d.Step(new(.52, Left: false));
        Check(drag.Dragging && Vector2.Distance(before, d.Renderer.DrawOffset) > 1, "A still-fading card does not block a map drag");
        d.Palette!.Open("a:"); d.Step(new(.7)); d.Step(new(.95));
        var point = d.Palette.PanelMin + new Vector2(100, 150); var scale = d.Config.MapScale;
        d.Step(new(.97, Mouse: point)); d.Step(new(.99, Wheel: -2)); d.Step(new(1.15));
        Check(d.Config.MapScale == scale, "List wheel never changes map zoom");
        c.InterfaceAnimations = InterfaceAnimationMode.Off;
        d.Step(new(1.2)); d.Palette.Close(); d.Step(new(1.21));
        Check(!d.Palette.CapturesInput && UiMotion.Store.Active == 0, "Off closes without animated or interactive leftovers");
        c.InterfaceAnimations = InterfaceAnimationMode.Full;
        d.Palette.Open("limsa"); d.Step(new(1.3)); d.Step(new(1.52));
        var field = ChromePolishVerification.Center(d.Palette.FieldBounds);
        d.Palette.Close(); d.Step(new(1.54, Mouse: field)); d.Step(new(1.56, Left: true));
        var fieldDrag = d.Step(new(1.58, Mouse: field + new Vector2(30, 25))); d.Step(new(1.60, Left: false));
        Check(fieldDrag.Dragging, "Collapsing search field releases the map beneath its final rect");
        c.InterfaceAnimations = InterfaceAnimationMode.Reduced; d.Step(new(1.8));
        Check(UiMotion.Value(0xfac39001, 1, 100, initial: 0) == 100, "Reduced suppresses spatial movement");
        Check(UiMotion.Value(0xfac39001, 0, 1, initial: 0, opacity: true) == 0, "Reduced starts a brief opacity-only fade");
        d.Step(new(1.83)); var reduced = UiMotion.Value(0xfac39001, 0, 1, opacity: true);
        Check(reduced > 0 && reduced < 1, "Reduced opacity has an intermediate state");
        d.Step(new(1.87)); Check(UiMotion.Value(0xfac39001, 0, 1, opacity: true) == 1, "Reduced opacity settles in 60ms");
        Console.WriteLine("Interface frames: travel enter/exit, immediate exit hit fallthrough, list wheel vs map zoom and Off.");
    }
    public static void Unfade()
    {
        foreach (var dt in new[] { .008, .016, .033 }) {
            var c = ChromeLayoutVerification.Config(new(640, 480)); c.ReduceMotion = false;
            c.RestoreOpacityOnHover = true; c.FadeMode = FadeMode.Always; c.FadePercent = .2f;
            using var d = new ScriptedFrameDriver(c);
            d.Step(new(0)); d.Step(new(.1));
            var point = c.WindowPosition + new Vector2(20, 150);
            d.Step(new(.2, Mouse: point));
            var entering = d.Step(new(.2 + dt));
            Check(entering.ChromeProgress >= 0 && d.Context.Chrome.ParentAlpha > .2f && d.Context.Chrome.ParentAlpha < 1, "Hover starts easing immediately without a delay");
            d.Step(new(.341)); Check(d.Context.Chrome.ParentAlpha == 1, "140ms hover restoration settles");
            d.Step(new(.36, Mouse: ChromePolishVerification.Center(MapToolbarDrawing.LastBounds)));
            Check(d.Context.Chrome.ParentAlpha == 1, "Crossing the toolbar/child boundary cannot flicker opacity");
            d.Step(new(.4, Mouse: new(-1000))); d.Step(new(.4 + dt));
            Check(d.Context.Chrome.ParentAlpha < 1 && d.Context.Chrome.ParentAlpha > .2f, "Leaving starts the 200ms fade immediately");
            d.Step(new(.601)); Check(MathF.Abs(d.Context.Chrome.ParentAlpha - .2f) < .0001f, "200ms exit settles");
            c.InterfaceAnimations = InterfaceAnimationMode.Off; d.Step(new(.62, Mouse: point));
            Check(d.Context.Chrome.ParentAlpha == 1, "Off hover restoration is immediate");
            c.InterfaceAnimations = InterfaceAnimationMode.Full; c.ReduceMotion = true;
            d.Step(new(.64, Mouse: new(-1000))); Check(d.Context.Chrome.ParentAlpha == .2f, "Reduce motion is immediate");
        }
        var cfg = new SystemConfig { GhostMode = true, GhostRestoreDelay = 10 };
        var ghost = new GhostMode(); ghost.Update(cfg, 0, true, false, false, false); ghost.Update(cfg, .3, true, false, false, false);
        ghost.Update(cfg, .4, false, true, false, false); ghost.Update(cfg, .41, false, true, false, false);
        Check(!ghost.ClickThrough && ghost.Opacity > .35f, "Ghost hover ignores the stationary click-through delay");
        ghost.Update(cfg, .541, false, true, false, false); Check(ghost.Opacity == 1, "Ghost uses the same 140ms token");
        ghost.Update(cfg, 1, true, false, false, false); ghost.Update(cfg, 1.3, true, false, false, false);
        ghost.Update(cfg, 1.4, false, false, false, false); ghost.Update(cfg, 1.41, false, false, false, false);
        Check(ghost.ClickThrough && ghost.Opacity > .35f, "Stationary input grace never delays opacity restoration");
        Console.WriteLine("Hover unfade: immediate 140/200ms ease at 8/16/33ms, whole-window child boundaries, Off/Reduce motion, ghost input-delay separation.");
    }
}
