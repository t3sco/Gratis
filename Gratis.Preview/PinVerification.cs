using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gratis.Controllers;
using Gratis.Data;
using Gratis.Rendering;
using ImGuiNET;

namespace Gratis.Preview;

internal static class PinVerification
{
    private static void Require(bool value,string message)=>TravelVerification.Require(value,message);
    private static string Forge(string json)
    {
        var bytes=Encoding.UTF8.GetBytes(json);
        return PinShareCode.Prefix+Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_')+"."+
            Convert.ToHexString(SHA256.HashData(bytes).AsSpan(0,8));
    }
    public static void ShareCodes()
    {
        var random=new Random(150024);
        for(var i=0;i<1000;i++) {
            var pin=new UserPin{MapId=(uint)random.Next(1,2000),X=(float)(random.NextDouble()*10000-5000),
                Y=(float)(random.NextDouble()*10000-5000),Colour=random.Next(8),
                Title=$"Meet {i} · 東京 café",Note=new string('x',random.Next(260))+"\n🎯"};
            pin=PinStore.Sanitize(pin);
            var code=PinShareCode.Encode(pin);
            Require(code.Length<PinShareCode.MaxLength && !code.Contains('+') && !code.Contains('/'),"URL-safe bounded pin code");
            Require(PinShareCode.TryDecode(code,out var decoded) && decoded!=null &&
                decoded.MapId==pin.MapId && decoded.X==pin.X && decoded.Y==pin.Y && decoded.Title==pin.Title &&
                decoded.Note==pin.Note && decoded.Colour==pin.Colour && decoded.Version==1,"share-code round trip");
            var at=random.Next(PinShareCode.Prefix.Length,code.LastIndexOf('.'));
            var damaged=code[..at]+(code[at]=='A'?'B':'A')+code[(at+1)..];
            Require(!PinShareCode.TryDecode(damaged,out _),"damaged code accepted");
            var malformed=new string(Enumerable.Range(0,random.Next(1,500)).Select(_=>(char)random.Next(0,65536)).ToArray());
            Require(!PinShareCode.TryDecode(i%2==0?malformed:PinShareCode.Prefix+malformed,out _),"random garbage accepted");
        }
        const string valid="{\"v\":1,\"m\":1,\"x\":2,\"y\":3,\"t\":\"Meeting\",\"n\":\"Note\",\"c\":0}";
        var invalid=new[]{"{}","[]","null",valid.Replace("\"v\":1","\"v\":2"),
            valid.Replace("\"m\":1","\"m\":0"),valid.Replace("\"m\":1","\"m\":100001"),
            valid.Replace("\"m\":1","\"m\":-1"),valid.Replace("\"m\":1","\"m\":4294967296"),
            valid.Replace("\"x\":2","\"x\":1e100"),valid.Replace("\"x\":2","\"x\":100001"),
            valid.Replace("\"y\":3","\"y\":-100001"),valid.Replace("\"y\":3","\"y\":\"3\""),
            valid.Replace("\"c\":0","\"c\":8"),valid.Replace("\"c\":0","\"c\":-1"),
            valid.Replace("\"n\":\"Note\"","\"n\":null"),valid.Replace("Meeting",new string('a',81)),
            valid.Replace("Note",new string('a',281)),valid.Replace("Meeting",""),
            valid.Replace("\"v\":1","\"v\":1,\"v\":1"),valid.Replace("\"v\":1","\"v\":1,\"command\":\"/li\""),
            valid.Replace("\"v\":1","\"v\":1,\"extra\":{\"deep\":{\"x\":1}}"),valid.Replace("\"v\":1,","")};
        foreach(var json in invalid) Require(!PinShareCode.TryDecode(Forge(json),out _),"invalid payload accepted: "+json[..Math.Min(json.Length,100)]);
        foreach(var code in new string?[]{null,"",PinShareCode.Prefix,PinShareCode.Prefix+new string('A',5000),"https://example.com",PinShareCode.Prefix+"=.0000000000000000"})
            Require(!PinShareCode.TryDecode(code,out _),"invalid envelope accepted");
        Require(PinShareCode.TryDecode(Forge(valid.Replace("Meeting","Meet\\u0000\\u202Eing")),out var sanitized) && sanitized!.Title=="Meeting","control/bidi sanitization");
        Console.WriteLine($"Pins: 1,000 exact Unicode round trips, 1,000 checksum mutations, 1,000 malformed fuzz inputs and {invalid.Length} forged invalid payloads; none throw or accept bad data.");
    }

    public static void Persistence()
    {
        var old=JsonSerializer.Deserialize<SystemConfig>("{}",ScriptedFrameDriver.Json)!;
        var oldStore=new PinStore(old,()=>{});
        Require(old.Pins && old.PinLabels==PinLabelMode.OnHover && old.PinMaxPerMap==200 && oldStore.ForMap(1).Count==0,"old config defaults");
        var config=new SystemConfig{PinMaxPerMap=2}; var saves=0;
        var store=new PinStore(config,()=>saves++);
        var pin=new UserPin{MapId=1,X=-40,Y=90,Title="Meet",Note=new string('a',400),Colour=2};
        Require(store.Put(pin)==PinChange.Saved && store.ForMap(1)[0].Note.Length==280,"note limit and save");
        Require(store.Put(pin with{Id=Guid.NewGuid()})==PinChange.Duplicate,"duplicate detection");
        Require(store.Put(pin with{Id=Guid.NewGuid(),Title="Second"})==PinChange.Saved,"second pin");
        Require(store.Put(pin with{Id=Guid.NewGuid(),Title="Third"})==PinChange.Limit,"per-map limit");
        Require(store.Put(pin with{Id=Guid.NewGuid(),MapId=2})==PinChange.Saved,"independent map limits");
        Require(store.Put(pin with{Title="Edited",Note="safe"})==PinChange.Saved,"edit at limit");
        var json=JsonSerializer.Serialize(config,ScriptedFrameDriver.Json);
        var loaded=JsonSerializer.Deserialize<SystemConfig>(json,ScriptedFrameDriver.Json)!;
        var restored=new PinStore(loaded,()=>{});
        Require(restored.ForMap(1).Count==2 && restored.ForMap(1)[0].Id==pin.Id && restored.ForMap(1)[0].Title=="Edited","persistence round trip");
        Require(saves==4,"mutations save exactly once; refused actions don't save");
        var corrupt=new SystemConfig{PinData=new(){[1]=[pin with{MapId=2},pin with{Colour=99},null!], [0]=[pin]}};
        _=new PinStore(corrupt,()=>{}); Require(corrupt.PinData.Count==0,"malformed saved pins pruned");
        var explicitNull=JsonSerializer.Deserialize<SystemConfig>("{\"PinData\":null,\"PinLabels\":99}",ScriptedFrameDriver.Json)!;
        _=new PinStore(explicitNull,()=>{}); Require(explicitNull.PinData!=null && explicitNull.PinLabels==PinLabelMode.OnHover,"null legacy data safe");
        loaded.AcceptedSpoilerWarning=true; loaded.WindowPosition=new(32); loaded.WindowSize=new(700,600); loaded.FadeMode=0;
        using(var driver=new ScriptedFrameDriver(loaded,new FrameScript())) {
            var layer=Attach(driver); driver.Step(new(0)); driver.Step(new(.02));
            Require(layer.Drawn==2,"reloaded pins render through shared frame");
        }
        old.AcceptedSpoilerWarning=true; old.WindowPosition=new(32); old.WindowSize=new(700,600); old.FadeMode=0;
        using(var driver=new ScriptedFrameDriver(old,new FrameScript())) {
            var layer=Attach(driver); driver.Step(new(0)); driver.Step(new(.02));
            Require(layer.Drawn==0 && layer.Store.ForMap(1).Count==0,"old config renders with empty pins");
        }
    }

    internal static PinOverlay Attach(ScriptedFrameDriver driver,Action? save=null)
    {
        var ui=driver.Context.Assistance=new MapAssistance(NullTravelProvider.Instance,new TravelFixture(driver),()=>true,()=>{});
        return ui.PinLayer=new PinOverlay(new PinStore(driver.Config,save??(()=>{})),place=>{
            if(place.World is {} world) driver.Renderer.BeginSpotlight(new(world,place.MapId,SpotlightSource.Pin));
        });
    }

    public static void Frames()
    {
        EditorLayouts();
        var config=PaletteVerification.Config(); var saves=0;
        using var driver=new ScriptedFrameDriver(config,new FrameScript{Palette=true});
        var layer=Attach(driver,()=>saves++);
        double time=0;
        void Step(Vector2? mouse=null,bool? left=null,ImGuiKey[]? keys=null,string? text=null,bool escape=false) {
            time+=.02; driver.Step(new(time,Mouse:mouse,Left:left,Keys:keys,Text:text,Escape:escape));
        }
        void Click(Vector2 point) { Step(point); Step(left:true); Step(left:false); }
        Step(); Step();
        layer.Edit(1,new(100,-60)); Step(); Step(); Step(text:"Meeting point"); Step(keys:[ImGuiKey.Enter]); Step();
        Require(layer.Store.ForMap(1).Count==1 && layer.Store.ForMap(1)[0].Title=="Meeting point","scripted add pin via editor/Enter");
        var pin=layer.Store.ForMap(1)[0];
        var screen=driver.Renderer.WorldToScreen(pin.World,driver.Config.WindowPosition);
        var world=driver.Renderer.ScreenToWorld(screen,driver.Config.WindowPosition);
        Require(Vector2.Distance(world,pin.World)<.001,"context-menu coordinate round trip");
        foreach(var lens in Lenses.BuiltIn) {
            config.ActiveLens=lens.Name; config.Markers.Declutter=true; Step();
            Require(layer.Drawn==1,"pins hidden by lens/declutter: "+lens.Name);
        }
        driver.Context.Visibility.Toggle(MarkerGroup.Pins); Step(); Require(layer.Drawn==0,"explicit Pins mute");
        driver.Context.Visibility.Toggle(MarkerGroup.Pins); Step(); Require(layer.Drawn==1,"Pins unmute");
        Step(layer.Positions[pin.Id]); Step(); Require(layer.CardVisible && layer.Buttons.ContainsKey("Edit") && layer.Buttons.ContainsKey("Delete"),"hover card actions");
        Click(layer.Buttons["Edit"]); Step(); Step(); Require(layer.CapturesInput && layer.TitleFocused,"hover Edit focuses editor title");
        Step(text:"Evening rendezvous"); Step(keys:[ImGuiKey.Enter]); Step();
        pin=layer.Store.ForMap(1)[0]; Require(pin.Title=="Evening rendezvous","scripted edit title: "+pin.Title);
        var code=PinShareCode.Encode(pin);
        Require(!layer.Import(code) && layer.Toast.Contains("already"),"duplicate import toast");
        Step(layer.Positions[pin.Id]); Step(); Click(layer.Buttons["Delete"]); Step();
        Require(layer.Store.ForMap(1).Count==0,"hover Delete persists removal");
        Require(layer.Import(code) && layer.CapturesInput && layer.Store.ForMap(1).Count==0,"paste requires import confirmation");
        Step(); Step(); Step(keys:[ImGuiKey.Enter]); Step();
        Require(layer.Store.ForMap(1).Count==1 && layer.Toast=="Pin imported","confirmed import toast");
        layer.Edit(1,new(50)); Step(); Step(escape:true); Step();
        Require(!layer.CapturesInput && layer.Store.ForMap(1).Count==1,"Esc discards editor");
        driver.Palette!.Open("Evening rendezvous"); Step(); Step();
        Require(driver.Palette.Results.Count==1 && driver.Palette.Results[0].Place.Kind==PlaceKind.Pin,"pin provider appears in palette");
        Step(keys:[ImGuiKey.Enter]); Step();
        Require(driver.Renderer.Spotlight.Active && driver.Renderer.Spotlight.Request!.Source==SpotlightSource.Pin,"palette Enter spotlights pin");
        config.Pins=false; Step(); Require(layer.Drawn==0,"Pins option off");
        config.Pins=true; config.VisualStyle=1; Step(); Require(layer.Drawn==0,"Classic unchanged");
        Require(saves==4,"add/edit/delete/import each persisted");
        Console.WriteLine("Pins: shared scripted add/edit/delete/import, hover actions, Enter/Esc, palette Spotlight, every lens, explicit mute, declutter, Classic and coordinate conversion verified.");
    }

    private static void EditorLayouts()
    {
        foreach(var size in new[]{new Vector2(360,280),new Vector2(780,600)})
        foreach(var scale in new[]{1f,1.5f}) {
            var config=ChromeLayoutVerification.Config(size); config.ChromeScale=scale;
            using var driver=new ScriptedFrameDriver(config,new FrameScript{Width=1000,Height=800});
            var layer=Attach(driver); layer.Edit(1,Vector2.Zero); layer.EditText("Rendezvous","Meet at the aetheryte after sunset.",0);
            driver.Step(new(0)); driver.Step(new(.04));
            var bounds=layer.EditorBounds; var center=ChromePolishVerification.Center(bounds);
            Require(bounds.X>=config.WindowPosition.X+14*scale && bounds.Z<=config.WindowPosition.X+size.X-14*scale &&
                bounds.Y>=config.WindowPosition.Y+14*scale && bounds.W<=config.WindowPosition.Y+size.Y-14*scale,"Editor obeys chrome inset at small/normal sizes and scales");
            driver.Step(new(.08,Mouse:center)); driver.Step(new(.12,Wheel:-20)); driver.Step(new(.16));
            foreach(var name in new[]{"Save pin","Cancel"}) {
                var action=layer.Buttons[name];
                Require(action.X>bounds.X && action.X<bounds.Z && action.Y>bounds.Y && action.Y<bounds.W,"Editor actions remain reachable after scrolling at small sizes");
            }
        }
    }
}
