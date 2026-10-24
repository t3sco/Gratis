using System.Numerics;
using System.Text.Json;
using Gratis.Controllers;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;

namespace Gratis.Preview;

internal sealed class FlagArrivalFixture : IFlagArrivalSource
{
    public FlagArrivalSample Sample = new(1, 1, new Vector2(20), new ArrivalFlag(1, 1, Vector2.Zero), "Middle La Noscea");
    public FlagArrivalSample Read() => Sample;
}

internal static class FlagArrivalVerification
{
    private static readonly ArrivalFlag Flag = new(1, 1, Vector2.Zero);
    private static int checks;
    private static void Require(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
    private static bool Tick(FlagArrivalState state, double time, Vector2? player = null, ArrivalFlag? flag = null,
        bool enabled = true, float radius = 4, uint map = 1, uint territory = 1, bool loading = false, long zone = 0) =>
        state.Update(time, territory, map, player ?? Vector2.Zero, flag ?? Flag, enabled, radius, loading, zone);
    private static FlagArrivalState Fresh() { var state = new FlagArrivalState(); Tick(state, 0, new Vector2(20)); return state; }
    private static bool Dwell(FlagArrivalState s, double start = .1, Vector2? player = null, float radius = 4, ArrivalFlag? flag = null)
    {
        Tick(s,start,player,flag,radius:radius); Tick(s,start+.5,player,flag,radius:radius); Tick(s,start+1,player,flag,radius:radius);
        return Tick(s,start+1.5,player,flag,radius:radius);
    }
    public static void State()
    {
        checks = 0;
        var s=Fresh(); Require(!Tick(s,.5,new(10)),"outside radius");
        s=Fresh(); Require(!Tick(s,.1),"entry only arms"); Require(!Tick(s,.6),"short dwell");
        Tick(s,1.1); Require(!Tick(s,1.599),"1.499 seconds is insufficient"); Require(Tick(s,1.6),"exact 1.5 seconds arrives");
        s=Fresh(); Require(Dwell(s,player:new(4,0)),"radius boundary included");
        s=Fresh(); Require(!Dwell(s,player:new(4.001f,0)),"just outside excluded");
        s=Fresh(); Tick(s,.1); Tick(s,.6,new(5,0)); Tick(s,1.1); Tick(s,1.6); Require(!Tick(s,2.1),"leaving resets dwell"); Require(Tick(s,2.6),"new full dwell arrives");
        s=Fresh(); Require(Dwell(s),"normal dwell"); Require(!Tick(s,1.8),"arrival debounced"); Tick(s,2,new(20)); Require(!Dwell(s,2.2),"exit/reentry never repeats same flag");
        s=Fresh(); Dwell(s); s.Update(1.8,1,1,Vector2.Zero,null); Require(!s.Arrived,"flag removal clears dimming"); Require(Dwell(s,2),"placing flag again rearms");
        s=Fresh(); Dwell(s); var replacement=Flag with { World=new(1,0) }; Require(Dwell(s,2,flag:replacement),"new flag position rearms");
        s=Fresh(); Dwell(s); Require(Dwell(s,2,flag:Flag with { Revision=1 }),"same-position setter revision rearms");
        s=Fresh(); Require(!Dwell(s,flag:Flag with { Map=2 }),"other-map flag never arrives");
        s=Fresh(); Require(!Dwell(s,flag:Flag with { Territory=2 }),"other-territory flag never arrives");
        s=Fresh(); Tick(s,.1); s.Update(.6,1,1,null,Flag); Require(!Tick(s,1.1),"missing player resets dwell");
        s=Fresh(); Require(!Tick(s,.1,new(float.NaN,0)),"NaN player rejected");
        s=Fresh(); Require(!Tick(s,.1,new(float.PositiveInfinity,0)),"infinite player rejected");
        s=Fresh(); Require(!Tick(s,.1,flag:Flag with { World=new(float.NaN,0) }),"invalid flag rejected");
        s=Fresh(); Require(!Tick(s,.1,enabled:false),"option off never fires"); Tick(s,.6,enabled:false); Require(!Tick(s,1.1),"enabling starts a fresh dwell");
        s=Fresh(); Tick(s,.1); Require(!Tick(s,.6,map:2,flag:Flag with { Map=2 }),"map change never fires");
        Require(!Tick(s,1.1,map:2,flag:Flag with { Map=2 }),"zone entry requires leaving radius");
        s=Fresh(); Tick(s,.1,territory:2,flag:Flag with { Territory=2 }); Require(!Tick(s,.6,territory:2,flag:Flag with { Territory=2 }),"territory change suppresses arrival");
        s=Fresh(); Tick(s,.1,loading:true); Require(!Tick(s,.6),"loading transition suppresses entry"); Tick(s,1.1,new(20)); Require(Dwell(s,1.6),"leaving after transition rearms");
        s=Fresh(); Tick(s,.1); Tick(s,.6); Require(!Tick(s,.2),"backwards clock cannot fire"); Require(!Tick(s,.7),"backwards clock resets dwell");
        s=Fresh(); Tick(s,.1); Require(!Tick(s,3),"large sample gap cannot satisfy dwell"); Require(!Tick(s,3.5),"gap restarts timer");
        s=Fresh(); Require(Dwell(s,radius:float.NaN),"invalid radius uses default"); s=Fresh(); Require(Dwell(s,radius:0),"zero radius uses default");
        s=Fresh(); Require(Dwell(s,player:new(6,0),radius:8),"custom radius applies");
        s=Fresh(); Require(!Tick(s,.1,map:0),"unknown current map"); s=Fresh(); Require(!Tick(s,.1,territory:0),"unknown current territory");
        s=Fresh(); Tick(s,.1); for(var i=0;i<10;i++) Tick(s,.1); Require(!s.Arrived && s.Dwell==0,"zero elapsed cannot accumulate");
        s=Fresh(); Require(!Tick(s,.1,zone:1),"territory event suppresses same-map reentry"); Require(!Tick(s,.6,zone:1),"same-map reentry remains disarmed");
        s=Fresh(); Require(!Tick(s,double.NaN),"invalid clock rejected");
        var config=JsonSerializer.Deserialize<SystemConfig>("{}",new JsonSerializerOptions{IncludeFields=true})!;
        Require(config.FlagArrival && !config.FlagAutoClear && config.FlagArrivalRadius==4,"legacy defaults");
        config.FlagArrival=false; config.FlagArrivalRadius=12; SettingsLayout.Reset(config,"Chrome & motion");
        Require(config.FlagArrival && config.FlagArrivalRadius==4,"settings reset");
        Console.WriteLine($"Flag arrival: {checks} assertions covering dwell/boundaries, debounce, resets, zones/loading, sample gaps, invalid inputs and legacy/reset settings.");
    }
    public static void Frames()
    {
        foreach(var reduced in new[]{false,true}) {
            var config=PaletteVerification.Config(); config.WindowSize=new(700,600); config.ReduceMotion=reduced;
            using var driver=new ScriptedFrameDriver(config,new FrameScript{Width=960,Height=800});
            var source=new FlagArrivalFixture(); driver.Context.FlagArrival.Source=source;
            driver.Step(new(0)); source.Sample=source.Sample with { Player=Vector2.Zero };
            for(var i=1;i<=17;i++) driver.Step(new(i*.1));
            Require(driver.Context.FlagArrival.State.Arrived && driver.Context.ContextMenu.Toast=="Arrived: Middle La Noscea","shared arrival toast");
            Require(driver.Context.FlagArrival.FlagAlpha==.45f,"retained native flag dims");
            driver.Step(new(1.8)); Require(driver.Context.FlagArrival.State.Decision=="already arrived","no repeated notification");
            config.FlagArrival=false; driver.Step(new(1.9)); Require(driver.Context.FlagArrival.FlagAlpha==1,"disabled restores flag");
            config.VisualStyle=1; driver.Step(new(2)); Require(!driver.Context.FlagArrival.State.Arrived,"Classic never arrives");
        }
        Console.WriteLine("Flag arrival: shared frames, toast, fallback dimming, Reduce Motion, option gating and Classic.");
    }
}
