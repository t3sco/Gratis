using System;
using System.Collections.Generic;
using System.Numerics;
using Gratis.Controllers;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed class PinOverlay(PinStore store, Action<PlaceEntry> show)
{
    public PinStore Store => store;
    public UserPin? Draft { get; private set; }
    public bool CapturesInput { get; private set; }
    public bool TitleFocused { get; private set; }
    public bool InputActive { get; private set; }
    public int Drawn { get; private set; }
    public bool CardVisible { get; private set; }
    public Vector4 EditorBounds { get; private set; }
    public string Toast { get; private set; } = "";
    public string LastShareCode { get; private set; } = "";
    public Dictionary<Guid, Vector2> Positions { get; } = [];
    public Dictionary<string, Vector2> Buttons { get; } = [];
    private UserPin? hover, retainedHover, retainedDraft;
    private Vector2 anchor;
    private double lastHover = double.NegativeInfinity, toastUntil;
    private bool focusTitle, importing;
    private string title = "", note = "", editorMessage = "";
    private int colour;
    private int editorSerial;
    private uint viewedMap;
    public static readonly Vector4[] Colours = [new(.35f,.79f,.84f,1),new(.37f,.63f,.96f,1),new(.53f,.54f,.94f,1),
        new(.74f,.52f,.91f,1),new(.94f,.56f,.73f,1),new(.94f,.48f,.42f,1),new(.94f,.74f,.35f,1),new(.48f,.79f,.55f,1)];
    private static readonly string[] ColourNames = ["Teal","Blue","Indigo","Violet","Rose","Coral","Amber","Green"];

    public void Edit(uint mapId, Vector2 world, UserPin? existing = null)
    {
        Draft = existing is null ? new() { MapId=mapId,X=world.X,Y=world.Y } : existing with { };
        title=Draft.Title; note=Draft.Note; colour=Draft.Colour;
        importing=false; focusTitle=true; editorMessage="";
        CapturesInput=true;
        editorSerial++;
    }
    public void CancelEditor() { Draft=null; importing=false; editorMessage=""; CapturesInput=false; }
    public void EditText(string newTitle,string newNote,int newColour) { title=newTitle; note=newNote; colour=newColour; }
    public PinChange SaveEditor()
    {
        if (Draft == null) return PinChange.Invalid;
        var updated = Draft with { Title=title,Note=note,Colour=colour };
        var result = store.Put(updated);
        if (result == PinChange.Saved) { Notify(importing ? "Pin imported" : "Pin saved"); CancelEditor(); hover=null; }
        else editorMessage=result switch { PinChange.Duplicate=>"This pin is already saved.",PinChange.Limit=>$"This map has reached its {store.Limit} pin limit.",_=>"This pin has invalid map data." };
        return result;
    }
    public bool Import(string? code)
    {
        if (!PinShareCode.TryDecode(code,out var pin) || pin == null) { Notify("Invalid pin share code"); return false; }
        if (store.Contains(pin)) { Notify("This pin is already saved"); return false; }
        Edit(pin.MapId,pin.World,pin); importing=true; return true;
    }
    public string Copy(UserPin pin)
    {
        LastShareCode=PinShareCode.Encode(pin); ImGui.SetClipboardText(LastShareCode); Notify("Pin share code copied"); return LastShareCode;
    }
    private void Notify(string message) { Toast=message; toastUntil=ImGui.GetTime()+3; }
    public void Spotlight(UserPin pin) { hover=null; show(PinStore.Entry(pin)); }

    public void DrawMarkers(IMapWindowHost host)
    {
        Positions.Clear(); Drawn=0;
        if (!host.Config.IsGratis || !host.Config.Pins) return;
        var context=MapDrawContext.Current; var s=context.GlobalScale;
        var mapId=host.Renderer.ViewedMapId;
        if (viewedMap != mapId) { viewedMap=mapId; hover=null; }
        var pins=store.ForMap(mapId);
        context.Visibility.Counts[(int)MarkerGroup.Pins]+=pins.Count;
        if (context.Visibility.State(MarkerGroup.Pins,true)==MarkerVisibilityState.Hidden) { hover=null; return; }
        var presentation=MarkerVisibility.Presentation(context.Visibility.State(MarkerGroup.Pins,true));
        var cursor=ImGui.GetCursorPos(); var list=ImGui.GetWindowDrawList();
        foreach (var pin in pins) {
            var point=host.Renderer.WorldToScreen(pin.World,ImGui.GetWindowPos());
            var center=point-new Vector2(0,10*s);
            if (!Classes.MapShape.ContainsSymbol(center,new Vector2(17*s,22*s))) continue;
            Positions[pin.Id]=center; Drawn++;
            var isHovered=false;
            if (!context.WindowMoveInputBlocked) {
                ImGui.SetCursorScreenPos(center-new Vector2(15*s));
                if (ImGui.InvisibleButton("##pin-"+pin.Id.ToString("N"),new Vector2(30*s))) Spotlight(pin);
                isHovered=ImGui.IsItemHovered();
                if (isHovered) { hover=pin; anchor=center; lastHover=ImGui.GetTime(); }
            }
            Symbol(list,center,13*s*presentation.Y,Colours[pin.Colour],isHovered,presentation.X);
            if (!context.ReportCapture && (host.Config.PinLabels==PinLabelMode.Always || host.Config.PinLabels==PinLabelMode.OnHover && isHovered)) {
                using var font=context.GetChromeFont(11*s,ChromeFontRole.Chip)?.Push();
                var size=TextDrawing.Measure(pin.Title)+new Vector2(12,6)*s;
                var position=center+new Vector2(19*s,-size.Y/2);
                if (Classes.MapShape.ContainsSymbol(position+size/2,size/2)) {
                    list.AddRectFilled(position,position+size,ImGui.GetColorU32(GratisTheme.Outline with{W=.95f}),5*s);
                    TextDrawing.Draw(list,position+new Vector2(6,3)*s,ImGui.GetColorU32(GratisTheme.Primary with { W=GratisTheme.Primary.W*presentation.X }),pin.Title);
                }
            }
        }
        ImGui.SetCursorPos(cursor); ImGui.Dummy(Vector2.Zero);
    }

    private static void Symbol(ImDrawListPtr list,Vector2 center,float radius,Vector4 colour,bool hovered,float alpha)
    {
        var s=MapDrawContext.Current.GlobalScale;
        for(var i=0;i<14;i++) {
            var angle=MathF.PI*(.75f-1.5f*i/13);
            list.PathLineTo(center+new Vector2(MathF.Sin(angle)*.82f,-MathF.Cos(angle)*.82f-.12f)*radius);
        }
        list.PathLineTo(center+new Vector2(0,1.05f*radius));
        list.PathFillConvex(ImGui.GetColorU32(Vector4.Lerp(GratisTheme.Outline with{W=1},colour,hovered?.85f:.6f) with { W=alpha }));
        for(var i=0;i<14;i++) {
            var angle=MathF.PI*(.75f-1.5f*i/13);
            list.PathLineTo(center+new Vector2(MathF.Sin(angle)*.82f,-MathF.Cos(angle)*.82f-.12f)*radius);
        }
        list.PathLineTo(center+new Vector2(0,1.05f*radius));
        list.PathStroke(ImGui.GetColorU32(colour with{W=.95f*alpha}),ImDrawFlags.Closed,1.4f*s);
        GratisIcons.Draw(list,center-new Vector2(0,2*s),GratisIcons.MapPin,new Vector4(1,1,1,.94f*alpha),16*s);
    }

    public void Draw(IMapWindowHost host,MapAssistance assistance)
    {
        Buttons.Clear(); InputActive=CardVisible=false; EditorBounds=Vector4.Zero;
        if (MapDrawContext.Current.ReportCapture) return;
        if (!host.Config.IsGratis || !host.Config.Pins) { CancelEditor(); hover=null; return; }
        var editorWasOpen=Draft!=null;
        if (Draft==null && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && !ImGui.GetIO().WantTextInput &&
            ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.V,false)) {
            var clipboard=ImGui.GetClipboardText();
            if (clipboard.StartsWith(PinShareCode.Prefix,StringComparison.Ordinal)) Import(clipboard);
        }
        if (Draft!=null) { retainedDraft=Draft; DrawEditor(host,assistance); }
        else {
            if (retainedDraft!=null && UiMotion.Visibility(ImGui.GetID("##pin-editor"),false)>.001f) DrawEditor(host,assistance,false);
            else retainedDraft=null;
            DrawCard(host,assistance);
        }
        CapturesInput=editorWasOpen || Draft!=null;
        var toastAlpha=UiMotion.Visibility(ImGui.GetID("##pin-notice"),Toast.Length>0 && ImGui.GetTime()<toastUntil);
        if (toastAlpha>.001f) {
            var s=MapDrawContext.Current.GlobalScale;
            var size=new Vector2(MathF.Min(310*s,ImGui.GetWindowWidth()*.65f),36*s);
            var pos=MapAssistance.ConstrainPanel(ImGui.GetWindowPos()+new Vector2((ImGui.GetWindowWidth()-size.X)/2,ImGui.GetWindowHeight()-130*s),size);
            using var font=MapDrawContext.Current.GetChromeFont(12*s,ChromeFontRole.Body)?.Push();
            using var motion=new UiMotion.VisualScope(ImGui.GetWindowDrawList(),pos,toastAlpha,
                UiMotion.Full?new Vector2(0,(1-toastAlpha)*6*s):Vector2.Zero);
            MapWindowDrawing.DrawSurface(ImGui.GetWindowDrawList(),pos,pos+size,8*s,surfaceOpacity:1);
            ImGui.GetWindowDrawList().AddText(pos+new Vector2(12,10)*s,ImGui.GetColorU32(GratisTheme.Primary),Toast);
        }
    }

    private void DrawCard(IMapWindowHost host,MapAssistance assistance)
    {
        var open=hover!=null && ImGui.GetTime()-lastHover<=.6;
        if(!open) hover=null;
        var pin=open?hover:retainedHover;
        var alpha=UiMotion.Visibility(ImGui.GetID("##pin-card"),open);
        if(pin==null || !open && alpha<=.001f) { retainedHover=null; return; }
        if(open) retainedHover=pin;
        var s=GratisTheme.ChromeScale;
        var width=MathF.Min(348*s,ImGui.GetWindowWidth()*(host.Config.MapCircle?.67f:.85f));
        if(width<210*s) return;
        float noteHeight;
        using(MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize*s,ChromeFontRole.Body)?.Push())
            noteHeight=pin.Note.Length==0?0:ImGui.CalcTextSize(pin.Note,false,width-24*s).Y;
        var size=new Vector2(width,MathF.Min(300*s,(136*s)+noteHeight));
        var pos=MapAssistance.ConstrainPanel(anchor+new Vector2(24,12)*s,size);
        if(ImGui.IsMouseHoveringRect(pos-new Vector2(12*s),pos+size+new Vector2(12*s),false)) lastHover=ImGui.GetTime();
        CardVisible=open;
        assistance.Panel("##pin-card",pos,size,()=>{
            using(MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize*s,ChromeFontRole.Title)?.Push())
                ImGui.TextUnformatted(ChromeControls.FitLabel(pin.Title,ImGui.GetContentRegionAvail().X));
            ImGui.PushTextWrapPos(ImGui.GetWindowWidth()-12*s);
            if(pin.Note.Length>0) {
                ImGui.PushStyleColor(ImGuiCol.Text,GratisTheme.Secondary);
                ImGui.TextUnformatted(pin.Note); ImGui.PopStyleColor();
            }
            ImGui.PopTextWrapPos(); ImGui.Spacing();
            if(Button("Edit")) Edit(pin.MapId,pin.World,pin);
            ImGui.SameLine(); if(Button("Spotlight")) Spotlight(pin);
            ImGui.SameLine(); if(Button("Delete")) { store.Delete(pin); hover=null; Notify("Pin deleted"); }
            if(Button("Copy share code")) Copy(pin);
            InputActive=open && (ImGui.IsWindowHovered() || ImGui.IsWindowFocused() && ImGui.IsAnyItemActive());
        },scroll:true,interactive:open);
    }

    private void DrawEditor(IMapWindowHost host,MapAssistance assistance,bool interactive=true)
    {
        var draft=Draft??retainedDraft;
        if(draft==null) return;
        var s=GratisTheme.ChromeScale;
        var (safeMin,safeMax)=ChromeLayout.SafeBounds();
        var size=Vector2.Min(new Vector2(360,348)*s,safeMax-safeMin);
        var pos=MapAssistance.ConstrainPanel(ImGui.GetWindowPos()+(ImGui.GetWindowSize()-size)/2,size);
        EditorBounds=interactive?new(pos.X,pos.Y,pos.X+size.X,pos.Y+size.Y):Vector4.Zero;
        assistance.Panel("##pin-editor",pos,size,()=>{
            InputActive=interactive;
            ImGui.PushStyleColor(ImGuiCol.FrameBg,new Vector4(1,1,1,.055f));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(7,5)*s);
            if(interactive) ImGui.SetNextFrameWantCaptureKeyboard(true);
            using(MapDrawContext.Current.GetChromeFont(GratisTheme.TextSize*s,ChromeFontRole.Title)?.Push()) ImGui.TextUnformatted(importing?"Import pin":"Pin details");
            ImGui.TextDisabled($"Map {draft.MapId} · {draft.X:0.0}, {draft.Y:0.0}");
            ImGui.Spacing(); ImGui.TextUnformatted("Title"); ImGui.SetNextItemWidth(-1);
            if(interactive && focusTitle) { ImGui.SetKeyboardFocusHere(); focusTitle=false; }
            ImGui.InputText("##pin-title-"+editorSerial,ref title,80,ImGuiInputTextFlags.AutoSelectAll);
            TitleFocused=interactive && ImGui.IsItemActive();
            ImGui.TextUnformatted("Note"); ImGui.SetNextItemWidth(-1);
            ImGui.InputTextMultiline("##pin-note-"+editorSerial,ref note,280,new Vector2(-1,78*s));
            ImGui.PopStyleVar(); ImGui.PopStyleColor();
            var pointer = draft.Pointer;
            if (ImGui.Checkbox("Edge pointer", ref pointer)) draft.Pointer = pointer;
            using(MapDrawContext.Current.GetChromeFont(GratisTheme.SmallTextSize*s,ChromeFontRole.Chip)?.Push())
                ImGui.TextDisabled(ChromeControls.FitLabel($"{note.Length}/280  ·  Enter saves  ·  Esc cancels",ImGui.GetContentRegionAvail().X));
            var columns=Math.Max(1,(int)((ImGui.GetContentRegionAvail().X+4*s)/(40*s)));
            for(var i=0;i<8;i++) {
                if(i%columns!=0) ImGui.SameLine(0,4*s);
                var min=ImGui.GetCursorScreenPos();
                if(ChromeControls.Button("##pin-colour-"+i,new Vector2(36*s))) colour=i;
                ChromeControls.Feedback(ImGui.GetWindowDrawList(),min,min+new Vector2(36*s));
                var center=min+new Vector2(18*s);
                ImGui.GetWindowDrawList().AddCircleFilled(center,8*s,ImGui.GetColorU32(Colours[i]),20);
                if(colour==i) ImGui.GetWindowDrawList().AddCircle(center,12*s,ImGui.GetColorU32(GratisTheme.Primary),24,s);
                if(ImGui.IsItemHovered()) UiMotion.TooltipText(ColourNames[i]);
            }
            ImGui.Spacing();
            var primary=importing?"Import pin":"Save pin";
            if(MapAssistance.PrimaryTravelButton(primary,GratisIcons.Check)) SaveEditor();
            Buttons[primary]=(ImGui.GetItemRectMin()+ImGui.GetItemRectMax())/2;
            if(ImGui.GetItemRectSize().X+ImGui.CalcTextSize("Cancel").X+24*s<ImGui.GetContentRegionAvail().X) ImGui.SameLine();
            if(Button("Cancel")) CancelEditor();
            if(editorMessage.Length>0) ImGui.TextColored(GratisTheme.Objective,editorMessage);
            if(interactive && ImGui.IsKeyPressed(ImGuiKey.Escape,false)) CancelEditor();
            else if(interactive && ImGui.IsKeyPressed(ImGuiKey.Enter,false)) SaveEditor();
        },scroll:true,interactive:interactive);
    }
    private bool Button(string text)
    {
        var result=MapAssistance.QuietButton(text);
        Buttons[text]=(ImGui.GetItemRectMin()+ImGui.GetItemRectMax())/2;
        return result;
    }
}
