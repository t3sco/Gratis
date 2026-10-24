using System.Numerics;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static partial class Program
{
    private static bool TryPinsScene(string[] args)
    {
        var at=Array.IndexOf(args,"--scene");
        if(at<0 || at+1>=args.Length || args[at+1] is not ("pins" or "pin-editor" or "pins-circular" or "pins-hover")) return false;
        var scene=args[at+1]; var outAt=Array.IndexOf(args,"--out");
        var output=Path.GetFullPath(outAt<0?Path.Combine("_preview",scene+".png"):args[outAt+1]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var config=PaletteVerification.Config(); config.MapCircle=scene=="pins-circular";
        config.WindowSize=config.MapCircle?new(680):new(820,620); config.PinLabels=PinLabelMode.Always;
        using var driver=new ScriptedFrameDriver(config,new FrameScript{Width=960,Height=800});
        var layer=PinVerification.Attach(driver);
        var titles=new[]{"Hunt rendezvous","Quiet fishing spot","Evening meet-up","Viewpoint","Gathering route","Favourite café","Treasure clue","Guild meeting"};
        for(var i=0;i<8;i++) layer.Store.Put(new(){MapId=1,X=-210+(i%4)*140,Y=i<4?-120:100,Colour=i,Title=titles[i],
            Note=i==0?"Meet by the west gate after the hunt.\nBring a friend; we'll leave at sunset.":"A little place worth remembering."});
        for(var i=0;i<5;i++) driver.Step(new(i*.02));
        if(scene=="pin-editor") {
            var pin=layer.Store.ForMap(1)[0]; layer.Edit(1,pin.World,pin);
            layer.EditText("Hunt rendezvous","Meet by the west gate after the hunt.\nBring a friend; we'll leave at sunset.",0);
        }
        var mouse=scene=="pins-hover"?layer.Positions[layer.Store.ForMap(1)[0].Id]:new Vector2(-1000);
        driver.Step(new(.10,Mouse:mouse)); driver.Step(new(.12)); driver.Step(new(.14));
        driver.Save(output); Console.WriteLine(output); return true;
    }
}
