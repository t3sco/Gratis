using System.Collections.Generic;
using Gratis.Classes;

namespace Gratis.Rendering;

public sealed class MapFrameTrace
{
    private int frame = -1;
    private readonly HashSet<uint> widgets = new(64);
    private readonly Dictionary<string, int> chrome = new(16);
    public HashSet<MarkerInfo> DrawnMarkers { get; } = new(64);
    public int TexturePasses { get; private set; }
    public int MotionAdvances { get; private set; }
    public int IdCollisions { get; private set; }
    public int DuplicateChrome { get; private set; }
    public int WidgetCount => widgets.Count;
    public void BeginFrame(int value)
    {
        if (frame == value) return;
        frame = value;
        TexturePasses = MotionAdvances = IdCollisions = DuplicateChrome = 0;
        widgets.Clear(); chrome.Clear(); DrawnMarkers.Clear();
    }
    public void TexturePass() => TexturePasses++;
    public void MotionAdvance() => MotionAdvances++;
    public void Widget(uint id) { if (!widgets.Add(id)) IdCollisions++; }
    public void Chrome(string name)
    {
        var count = chrome.GetValueOrDefault(name) + 1;
        chrome[name] = count;
        if (count > 1) DuplicateChrome++;
    }
    public int ChromeCount(string name) => chrome.GetValueOrDefault(name);
}
