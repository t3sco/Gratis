using System.Globalization;
using System.Numerics;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class CoordinateVerification
{
    public static void Run()
    {
        static void Near(Vector2 actual, Vector2 expected, string label) {
            if (Vector2.Distance(actual, expected) > .002f) throw new InvalidOperationException(label + $": {actual} != {expected}");
        }
        var cases = new (Vector2 World, Vector2 Offset, float Factor, Vector2 Expected)[] {
            (new(0), new(0), 1, new(21.5f)),
            (new(-1024), new(0), 1, new(1)),
            (new(1024), new(0), 1, new(42)),
            (new(0), new(0), 2, new(11.25f)),
            (new(-512,512), new(0), 2, new(1,21.5f)),
            (new(-100,200), new(100,-200), 1, new(21.5f)),
            (new(924,-824), new(100,-200), 1, new(42,1)),
            (new(0), new(0), .5f, new(42)),
            (new(0), new(0), 4, new(6.125f)),
        };
        foreach (var c in cases) {
            Near(CoordinateShare.WorldToMap(c.World,c.Offset,c.Factor),c.Expected,"known scale/offset sample");
            Near(CoordinateShare.MapToWorld(c.Expected,c.Offset,c.Factor),c.World,"known sample inverse");
        }
        var random = new Random(8401);
        foreach (var factor in new[] {.5f, .75f, 1, 1.25f, 2, 4})
            for (var i=0;i<1000;i++) {
                var world = new Vector2(random.Next(-2000,2000),random.Next(-2000,2000));
                var offset = new Vector2(random.Next(-500,500),random.Next(-500,500));
                Near(CoordinateShare.MapToWorld(CoordinateShare.WorldToMap(world,offset,factor),offset,factor),world,"world-map-world round trip");
            }
        var culture = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            if (CoordinateShare.Text(new(21.3f,14.8f)) != "21.3, 14.8" ||
                CoordinateShare.Text(new(21.3f,14.8f),"La Noscea") != "La Noscea · 21.3, 14.8") throw new InvalidOperationException("invariant coordinate copy");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        Console.WriteLine("Coordinates: 9 analytic scale/offset fixtures, inverses, 6,000 round trips and invariant copy formats.");
    }
    public static void Frames()
    {
        var config = PaletteVerification.Config(); config.WindowSize = new(780,600);
        using var driver = new ScriptedFrameDriver(config, new FrameScript { Width=960,Height=800, PlayerCoordinates=new(21.3f,14.8f) });
        driver.Context.ContextMenu.Actions = new ContextMenuFixture();
        for(var i=0;i<5;i++) driver.Step(new(i*.02));
        var b=MapWindowDrawing.CoordinateBounds;
        var point=new Vector2(b.X+40,(b.Y+b.W)/2);
        driver.Step(new(.12,Mouse:point)); driver.Step(new(.14,Left:true)); driver.Step(new(.16,Left:false));
        if(driver.Context.ContextMenu.Toast!="Copied 21.3, 14.8" || driver.Window.Dragging) throw new InvalidOperationException("coordinate click copies exact displayed value without pan");
        driver.Step(new(.18,Shift:true,Left:true)); driver.Step(new(.20,Left:false));
        if(driver.Context.ContextMenu.Toast!="Copied Middle La Noscea · 21.3, 14.8") throw new InvalidOperationException("Shift-click includes map name");
        config.ShowEorzeaTime=true;
        driver.Step(new(.22));
        var copied=CoordinateCopy.PlayerBounds;
        var entire=MapWindowDrawing.CoordinateBounds;
        if(copied.Z>=entire.Z) throw new InvalidOperationException("ET suffix must be outside coordinate copy targets");
        Console.WriteLine("Coordinate bar: actual scripted click/Shift-click, toast, pan isolation and ET bounds.");
    }
}
