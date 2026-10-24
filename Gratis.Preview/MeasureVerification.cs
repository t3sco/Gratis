using System.Numerics;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class MeasureVerification
{
    private static void Require(bool yes, string message) { if (!yes) throw new InvalidOperationException(message); }
    public static void Maths()
    {
        Require(MeasureToolMath.SegmentDistance(new(5,3),new(0,0),new(10,0))==3,"Segment perpendicular distance");
        Require(MeasureToolMath.SegmentDistance(new(13,4),new(0,0),new(10,0))==5,"Segment end distance");
        Require(MeasureToolMath.SegmentDistance(new(3,4),default,default)==5,"Zero-length ruler");
        foreach(var circle in new[]{false,true}) {
            var min=new Vector2(50); var max=new Vector2(450); var center=(min+max)/2;
            for(var i=0;i<360;i++) {
                var direction=new Vector2(MathF.Cos(i*MathF.PI/180),MathF.Sin(i*MathF.PI/180));
                Require(MeasureToolMath.Clip(center-direction*900,center+direction*900,min,max,circle?200:14,3,out var a,out var b),"Crossing ruler clipped");
                Require(MeasureToolMath.Depth(a,min,max,circle?200:14)>=2.99f && MeasureToolMath.Depth(b,min,max,circle?200:14)>=2.99f,"Clipped ink inside shape");
                Require(MeasureToolMath.SegmentDistance(center,a,b)<.01f,"Clipped line stays collinear");
            }
            Require(!MeasureToolMath.Clip(new(-30),new(-20),min,max,circle?200:14,3,out _,out _),"Off-frame segment omitted");
            Require(MeasureToolMath.Clip(center,center,min,max,circle?200:14,3,out _,out _),"Zero ruler inside");
        }
    }
    public static void Frames()
    {
        foreach(var circle in new[]{false,true}) {
            var config=PaletteVerification.Config(); config.MapCircle=circle; config.EnableShiftDragMove=true;
            var script=new FrameScript {MapSizeFactor=.75f,MapOffset=new(40,-25),Markers=[new(60460,new(-100,-50)),new(60436,new(150,80))]};
            using var driver=new ScriptedFrameDriver(config,script);
            var markerActions=0; driver.Markers[0].OnLeftClicked=()=>markerActions++;
            driver.Step(new(0)); driver.Step(new(.02));
            var measure=driver.Context.Measure; var origin=config.WindowPosition;
            var a=driver.Renderer.WorldToScreen(new(-100,-50),origin); var b=driver.Renderer.WorldToScreen(new(150,80),origin);
            var offset=driver.Renderer.DrawOffset;
            driver.Step(new(.04,Mouse:a+new Vector2(4,0),Left:true,Control:true,Shift:true));
            Require(measure.Active && measure.Dragging && measure.StartSnapped,"Ctrl starts snapped measure: "+measure.Diagnostics());
            Require(markerActions==0,"Measure must not invoke marker actions");
            var dragging=driver.Step(new(.06,Mouse:b+new Vector2(3,0)));
            Require(measure.EndSnapped && !dragging.Dragging && !dragging.MovingWindow && dragging.Offset==offset,"Measure owns gesture over pan/window move");
            driver.Step(new(.08,Left:false,Control:false,Shift:false));
            Require(measure.Active && !measure.Dragging && MathF.Abs(measure.Distance-new Vector2(250,130).Length())<.01f,"World distance persists after release");
            var start=measure.Start; var end=measure.End;
            var zoomed=driver.Step(new(.10,Mouse:origin+config.WindowSize/2,Wheel:1));
            Require(measure.Start==start && measure.End==end,"Zoom preserves world anchors");
            driver.Renderer.PanViewport(new(30,-10)); driver.Step(new(.12));
            Require(measure.Start==start && measure.End==end,"Viewport pan preserves world anchors");
            var coordinate=driver.Renderer.WorldToMapCoordinates(start);
            var expected=Vector2.One+(new Vector2(1024)+(start-script.MapOffset)*script.MapSizeFactor)*(41/(2048*script.MapSizeFactor));
            Require(Vector2.Distance(coordinate,expected)<.0001f,"Map coordinates include size factor and offset");
            var chip=(measure.ChipMin+measure.ChipMax)/2;
            driver.Step(new(.14,Mouse:chip)); driver.Step(new(.16,Right:true));
            Require(measure.LastCopy.EndsWith('y') && !driver.Context.ContextMenu.IsOpen,"Right-click copies distance without map menu");
            driver.Step(new(.18,Right:false));
            driver.Step(new(.19,Mouse:origin+config.WindowSize*new Vector2(.2f,.7f),Left:true));
            Require(!measure.Active,"Click elsewhere clears");
            driver.Step(new(.195,Left:false)); measure.Begin(driver.MapId,Vector2.Zero);
            driver.Step(new(.20,Escape:true)); Require(!measure.Active,"Esc clears");
            driver.Step(new(.22,Escape:false,Mouse:a,Control:true,Alt:true,Left:true)); Require(!measure.Active,"Alt keeps window-move precedence");
            driver.Step(new(.24,Alt:false,Control:false,Left:false));
            config.MeasureTool=false; driver.Step(new(.26,Mouse:a,Control:true,Left:true)); Require(!measure.Active,"Setting disables ruler");
            config.MeasureTool=true; config.VisualStyle=1; driver.Step(new(.28,Left:false)); driver.Step(new(.30,Left:true)); Require(!measure.Active,"Classic unchanged");
        }
    }
}
