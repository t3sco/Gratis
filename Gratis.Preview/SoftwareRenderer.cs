using System.Numerics;
using ImGuiNET;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Gratis.Preview;

internal sealed class Textures
{
    private readonly Dictionary<nint, Texture> textures = [];
    private long nextId;

    public nint Add(int width, int height, byte[] rgba)
    {
        if (rgba.Length != checked(width * height * 4)) throw new ArgumentException("Invalid RGBA texture length.");
        var id = (nint)(++nextId);
        textures.Add(id, new Texture(width, height, rgba));
        return id;
    }

    public Texture Get(nint id) => textures.TryGetValue(id, out var texture)
        ? texture : throw new InvalidOperationException($"Unregistered ImGui texture {id}.");

    public nint Load(string path)
    {
        using var codec = SKCodec.Create(path) ?? throw new InvalidDataException($"Cannot decode PNG {path}.");
        using var image = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        if (codec.GetPixels(image.Info, image.GetPixels()) != SKCodecResult.Success) throw new InvalidDataException("PNG conversion failed.");
        var bytes = new byte[checked(image.Width * image.Height * 4)];
        Marshal.Copy(image.GetPixels(), bytes, 0, bytes.Length);
        return Add(image.Width, image.Height, bytes);
    }
}

internal sealed record Texture(int Width, int Height, byte[] Pixels)
{
    public Vector4 Sample(Vector2 uv)
    {
        var x = Math.Clamp(uv.X * Width - 0.5f, 0, Width - 1);
        var y = Math.Clamp(uv.Y * Height - 0.5f, 0, Height - 1);
        var x0 = (int)x;
        var y0 = (int)y;
        var x1 = Math.Min(x0 + 1, Width - 1);
        var y1 = Math.Min(y0 + 1, Height - 1);
        return Vector4.Lerp(Vector4.Lerp(Pixel(x0, y0), Pixel(x1, y0), x - x0),
            Vector4.Lerp(Pixel(x0, y1), Pixel(x1, y1), x - x0), y - y0);
    }

    private Vector4 Pixel(int x, int y)
    {
        var i = (y * Width + x) * 4;
        return new Vector4(Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]) / 255.0f;
    }
}

internal sealed class SoftwareRenderer(int width, int height, Textures textures)
{
    public static readonly SKColor Background = new(22, 25, 31, 255);
    private readonly Vector4[] pixels = Enumerable.Repeat(
        new Vector4(Background.Red, Background.Green, Background.Blue, Background.Alpha) / 255.0f, checked(width * height)).ToArray();

    public int Triangles { get; private set; }
    public int BlurCount { get; private set; }
    internal Vector4 Pixel(int x, int y) => pixels[y * width + x];

    public unsafe void Render(ImDrawDataPtr data, IReadOnlyList<BackdropBlur>? blurs = null)
    {
        for (var listIndex = 0; listIndex < data.CmdListsCount; listIndex++) {
            var list = data.CmdLists[listIndex];
            var pending = new Queue<BackdropBlur>((blurs ?? []).Where(b => b.List == (nint)list.NativePtr).OrderBy(b => b.IndexOffset));
            for (var commandIndex = 0; commandIndex < list.CmdBuffer.Size; commandIndex++) {
                var command = list.CmdBuffer[commandIndex];
                if (command.UserCallback != 0) {
                    if (command.UserCallback is -8 or -1) continue;
                    throw new NotSupportedException($"Custom ImDraw callback {command.UserCallback} is not supported.");
                }
                var texture = textures.Get(command.TextureId);
                var clipMin = (new Vector2(command.ClipRect.X, command.ClipRect.Y) - data.DisplayPos) * data.FramebufferScale;
                var clipMax = (new Vector2(command.ClipRect.Z, command.ClipRect.W) - data.DisplayPos) * data.FramebufferScale;
                var minX = Math.Clamp((int)MathF.Ceiling(clipMin.X - .5f), 0, width);
                var minY = Math.Clamp((int)MathF.Ceiling(clipMin.Y - .5f), 0, height);
                var maxX = Math.Clamp((int)MathF.Ceiling(clipMax.X - .5f), 0, width);
                var maxY = Math.Clamp((int)MathF.Ceiling(clipMax.Y - .5f), 0, height);
                for (uint index = 0; index + 2 < command.ElemCount; index += 3) {
                    var i = checked((int)(command.IdxOffset + index));
                    while (pending.TryPeek(out var blur) && blur.IndexOffset <= i) {
                        Blur(pending.Dequeue(), data.DisplayPos, data.FramebufferScale);
                    }
                    var offset = checked((int)command.VtxOffset);
                    var a = *list.VtxBuffer[offset + list.IdxBuffer[i]].NativePtr;
                    var b = *list.VtxBuffer[offset + list.IdxBuffer[i + 1]].NativePtr;
                    var c = *list.VtxBuffer[offset + list.IdxBuffer[i + 2]].NativePtr;
                    a.pos = (a.pos - data.DisplayPos) * data.FramebufferScale;
                    b.pos = (b.pos - data.DisplayPos) * data.FramebufferScale;
                    c.pos = (c.pos - data.DisplayPos) * data.FramebufferScale;
                    Triangle(a, b, c, texture, minX, minY, maxX, maxY);
                    Triangles++;
                }
            }
            while (pending.TryDequeue(out var blur)) Blur(blur, data.DisplayPos, data.FramebufferScale);
        }
    }

    private void Blur(BackdropBlur request, Vector2 displayPos, Vector2 scale)
    {
        var min = (request.Min - displayPos) * scale;
        var max = (request.Max - displayPos) * scale;
        var x0 = Math.Clamp((int)MathF.Floor(min.X), 0, width);
        var x1 = Math.Clamp((int)MathF.Ceiling(max.X), 0, width);
        var y0 = Math.Clamp((int)MathF.Floor(min.Y), 0, height);
        var y1 = Math.Clamp((int)MathF.Ceiling(max.Y), 0, height);
        if (x0 >= x1 || y0 >= y1) return;
        var radius = Math.Clamp((int)MathF.Ceiling(request.Radius * scale.X), 1, 128);
        var rounding = request.Rounding * scale.X;
        var kernel = radius * 2 + 1;
        var sy0 = Math.Max(0, y0 - radius);
        var sy1 = Math.Min(height, y1 + radius);
        var span = x1 - x0;
        var horizontal = new Vector4[span * (sy1 - sy0)];
        for (var y = sy0; y < sy1; y++) {
            var sum = Vector4.Zero;
            for (var x = x0 - radius; x <= x0 + radius; x++) sum += pixels[y * width + Math.Clamp(x, 0, width - 1)];
            for (var x = x0; x < x1; x++) {
                horizontal[(y - sy0) * span + x - x0] = sum / kernel;
                sum += pixels[y * width + Math.Clamp(x + radius + 1, 0, width - 1)] -
                       pixels[y * width + Math.Clamp(x - radius, 0, width - 1)];
            }
        }
        var center = (min + max) / 2;
        var half = (max - min) / 2;
        rounding = Math.Clamp(rounding, 0, MathF.Min(half.X, half.Y));
        for (var x = x0; x < x1; x++) {
            var sum = Vector4.Zero;
            for (var y = y0 - radius; y <= y0 + radius; y++)
                sum += horizontal[(Math.Clamp(y, 0, height - 1) - sy0) * span + x - x0];
            for (var y = y0; y < y1; y++) {
                var q = Vector2.Abs(new Vector2(x + .5f, y + .5f) - center) - half + new Vector2(rounding);
                var distance = Vector2.Max(q, Vector2.Zero).Length() + MathF.Min(MathF.Max(q.X, q.Y), 0) - rounding;
                var coverage = Math.Clamp(.5f - distance, 0, 1);
                ref var target = ref pixels[y * width + x];
                target = Vector4.Lerp(target, sum / kernel, coverage * request.Opacity);
                if (y + 1 < y1)
                    sum += horizontal[(Math.Clamp(y + radius + 1, 0, height - 1) - sy0) * span + x - x0] -
                           horizontal[(Math.Clamp(y - radius, 0, height - 1) - sy0) * span + x - x0];
            }
        }
        BlurCount++;
    }

    private static float Edge(Vector2 a, Vector2 b, Vector2 p) =>
        (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

    private static bool TopLeft(Vector2 a, Vector2 b) => b.Y < a.Y || b.Y == a.Y && b.X > a.X;
    private static bool Inside(float edge, bool topLeft) => edge > 0 || edge == 0 && topLeft;

    private static Vector4 Color(uint color) => new Vector4(color & 255, color >> 8 & 255, color >> 16 & 255, color >> 24) / 255.0f;

    private void Triangle(ImDrawVert a, ImDrawVert b, ImDrawVert c, Texture texture, int clipX0, int clipY0, int clipX1, int clipY1)
    {
        var area = Edge(a.pos, b.pos, c.pos);
        if (MathF.Abs(area) < 1e-6f) return;
        if (area < 0) { (b, c) = (c, b); area = -area; }
        var min = Vector2.Min(a.pos, Vector2.Min(b.pos, c.pos));
        var max = Vector2.Max(a.pos, Vector2.Max(b.pos, c.pos));
        var x0 = Math.Max(clipX0, (int)MathF.Ceiling(min.X - .5f));
        var y0 = Math.Max(clipY0, (int)MathF.Ceiling(min.Y - .5f));
        var x1 = Math.Min(clipX1, (int)MathF.Ceiling(max.X - .5f));
        var y1 = Math.Min(clipY1, (int)MathF.Ceiling(max.Y - .5f));
        var edgeA = TopLeft(b.pos, c.pos);
        var edgeB = TopLeft(c.pos, a.pos);
        var edgeC = TopLeft(a.pos, b.pos);
        var ca = Color(a.col);
        var cb = Color(b.col);
        var cc = Color(c.col);
        for (var y = y0; y < y1; y++) {
            for (var x = x0; x < x1; x++) {
                var point = new Vector2(x + .5f, y + .5f);
                var wa = Edge(b.pos, c.pos, point);
                var wb = Edge(c.pos, a.pos, point);
                var wc = Edge(a.pos, b.pos, point);
                if (!Inside(wa, edgeA) || !Inside(wb, edgeB) || !Inside(wc, edgeC)) continue;
                wa /= area; wb /= area; wc /= area;
                var uv = a.uv * wa + b.uv * wb + c.uv * wc;
                var source = texture.Sample(uv) * (ca * wa + cb * wb + cc * wc);
                ref var destination = ref pixels[y * width + x];
                var rgb = new Vector3(source.X, source.Y, source.Z) * source.W +
                          new Vector3(destination.X, destination.Y, destination.Z) * (1 - source.W);
                destination = new Vector4(rgb, source.W + destination.W * (1 - source.W));
            }
        }
    }

    public void Save(string path)
    {
        var bytes = new byte[checked(width * height * 4)];
        for (var i = 0; i < pixels.Length; i++) {
            var color = Vector4.Clamp(pixels[i], Vector4.Zero, Vector4.One) * 255.0f;
            bytes[i * 4] = (byte)MathF.Round(color.X);
            bytes[i * 4 + 1] = (byte)MathF.Round(color.Y);
            bytes[i * 4 + 2] = (byte)MathF.Round(color.Z);
            bytes[i * 4 + 3] = (byte)MathF.Round(color.W);
        }
        using var image = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        Marshal.Copy(bytes, 0, image.GetPixels(), bytes.Length);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using (var output = File.Create(path)) {
            if (!image.Encode(output, SKEncodedImageFormat.Png, 100)) throw new IOException("PNG encoding failed.");
        }
        using var saved = SKBitmap.Decode(path) ?? throw new IOException("Cannot read saved PNG.");
        var changed = 0L;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (saved.GetPixel(x, y) != Background) changed++;
        Console.WriteLine($"{Path.GetFullPath(path)}: {width}x{height}, {Triangles} triangles, non-background {100.0 * changed / pixels.Length:F2}%");
        foreach (var point in new[] { (0, 0), (width / 2, height / 2), (width / 4, height / 4), (width * 3 / 4, height * 3 / 4) }) {
            var p = saved.GetPixel(point.Item1, point.Item2);
            Console.WriteLine($"  ({point.Item1},{point.Item2}) RGBA=({p.Red},{p.Green},{p.Blue},{p.Alpha})");
        }
        if (changed < pixels.Length / 100) throw new InvalidOperationException("Rendered PNG is unexpectedly blank (<1% non-background pixels).");
    }
}
