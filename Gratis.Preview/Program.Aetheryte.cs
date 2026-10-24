using Gratis.Rendering;

namespace Gratis.Preview;

internal static partial class Program
{
    private static bool TryAetheryteScene(string[] args)
    {
        var at=Array.IndexOf(args,"--scene");
        if(at<0 || at+1>=args.Length || args[at+1]!="aetheryte-chip") return false;
        var outAt=Array.IndexOf(args,"--out");
        var output=Path.GetFullPath(outAt<0?"_preview/aetheryte-chip.png":args[outAt+1]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var config=PaletteVerification.Config(); config.WindowSize=new(820,620);
        using var driver=new ScriptedFrameDriver(config,new FrameScript{Width=960,Height=740});
        driver.Context.Assistance=new MapAssistance(NullTravelProvider.Instance,new TravelFixture(driver),()=>true,()=>{});
        driver.Step(new(0)); driver.Step(new(.02));
        driver.Renderer.BeginSpotlight(new(new(500,-150),1,SpotlightSource.Quest,IncludePlayer:false));
        for(var i=2;i<20;i++) driver.Step(new(i*.02));
        driver.Save(output); Console.WriteLine(output); return true;
    }
}
