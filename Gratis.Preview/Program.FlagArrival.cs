using System.Numerics;

namespace Gratis.Preview;

internal static partial class Program
{
    private static bool TryFlagArrivalScene(string[] args)
    {
        var at=Array.IndexOf(args,"--scene");
        if(at<0 || at+1>=args.Length || args[at+1]!="flag-arrival") return false;
        var outAt=Array.IndexOf(args,"--out");
        var output=Path.GetFullPath(outAt<0?"_preview/flag-arrival.png":args[outAt+1]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var config=PaletteVerification.Config(); config.WindowSize=new(700,600); config.ReduceMotion=false; config.Motion.PlayerTrail=false;
        using var driver=new ScriptedFrameDriver(config,new FrameScript{Width=960,Height=800, Markers=[new(60561,Vector2.Zero)]});
        driver.Markers[0] = new Gratis.Classes.MarkerInfo { Position=Vector2.Zero, Offset=Vector2.Zero, Scale=1, IconId=60561, IsFlag=true };
        var source=new FlagArrivalFixture(); driver.Context.FlagArrival.Source=source;
        driver.Step(new(0)); source.Sample=source.Sample with { Player=Vector2.Zero };
        for(var i=1;i<=18;i++) driver.Step(new(i*.1));
        driver.Save(output); Console.WriteLine(output); return true;
    }
}
