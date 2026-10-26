using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;
using Gratis.Rendering;

namespace Gratis.Preview;

internal static class RadiusVerification {
    public static void Run() {
        if (GratisTheme.Radius.Control != 6 || GratisTheme.Radius.Card != 8 || GratisTheme.Radius.Popup != 8
            || GratisTheme.CardRadius != 8 || GratisTheme.ButtonRadius != 6
            || GratisTheme.Radius.Inner(8, 4) != 4 || GratisTheme.Radius.Inner(6, 9) != 0
            || GratisTheme.Radius.Pill(new(36, 4)) != 2 || GratisTheme.Radius.Pill(new(-1, 4)) != 0)
            throw new InvalidOperationException("Corner radius scale changed.");
        var exceptions = new HashSet<(string File, string Radius)> {
            ("ContextualMarkerDrawing.cs", "2.5f * stroke"), ("ContextualMarkerDrawing.cs", "0"),
            ("FlatMarkerDrawing.cs", "2 * stroke"),
            ("MapToolbarDrawing.Glyphs.cs", "2 * scale"), ("MapToolbarDrawing.Glyphs.cs", "1.5f * scale"),
            ("LensDrawing.cs", "MathF.Max(0, Classes.MapShape.Current - inset)"),
            ("MapWindowDrawing.cs", "MathF.Max(0.0f, rounding - half)"),
        };
        var assembly = Assembly.GetExecutingAssembly();
        var sources = assembly.GetManifestResourceNames().Where(n => n.StartsWith("RadiusSource.")).ToArray();
        if (sources.Length == 0)
            throw new InvalidOperationException("Missing radius source fixtures.");
        foreach (var name in sources) {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            CheckSource(name["RadiusSource.".Length..], reader.ReadToEnd(), exceptions);
        }
        try {
            CheckSource("NewChrome.cs", "list.AddRectFilled(min, max, color, 5 * scale);", exceptions);
            throw new InvalidOperationException("Literal radius guard did not reject a new chrome radius.");
        }
        catch (ArgumentException) { }
        Console.WriteLine($"Corner radii: tokens, nesting, pills and {sources.Length} rendering sources verified.");
    }

    private static void CheckSource(string file, string source, HashSet<(string File, string Radius)> exceptions) {
        var text = Regex.Replace(source, "//[^\\r\\n]*|/\\*[\\s\\S]*?\\*/|\"(?:\\\\.|[^\"\\\\])*\"", "\"\"");
        foreach (Match match in Regex.Matches(text, @"\.AddRect(?:Filled)?\(")) {
            var arguments = new List<string>();
            var start = match.Index + match.Length;
            var depth = 0;
            for (var i = start; i < text.Length; i++) {
                var c = text[i];
                if (c == ')' && depth == 0) {
                    arguments.Add(text[start..i]);
                    break;
                }
                if (c is '(' or '[' or '{') depth++;
                if (c is ')' or ']' or '}') depth--;
                if (c == ',' && depth == 0) {
                    arguments.Add(text[start..i]);
                    start = i + 1;
                }
            }
            if (arguments.Count < 4)
                continue;
            var radius = Regex.Replace(arguments[3].Trim(), @"\s+", " ");
            if (Regex.IsMatch(radius, @"\d") && !radius.Contains("GratisTheme.Radius.")
                && !exceptions.Contains((file, radius)))
                throw new ArgumentException($"{file}: use a radius token instead of '{radius}'.");
        }
    }
}
