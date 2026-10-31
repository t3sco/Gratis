using System;
using System.Collections.Generic;
using System.Text;

namespace Gratis.Rendering;

/// <summary>Title case for place and NPC names the game stores in lowercase. Results are cached, so drawing allocates once.</summary>
public static class QuestLogTitleCase {
    private static readonly HashSet<string> Small = new(StringComparer.OrdinalIgnoreCase)
        { "of", "the", "and", "in", "on", "at", "to", "for", "a", "an", "by", "de", "du", "la", "le" };
    private static readonly Dictionary<string, string> Cache = [];

    public static string Of(string text) {
        if (text.Length == 0)
            return text;
        if (Cache.TryGetValue(text, out var cached))
            return cached;
        if (Cache.Count >= 256)
            Cache.Clear();
        return Cache[text] = Convert(text);
    }

    private static string Convert(string text) {
        var result = new StringBuilder(text.Length);
        var start = true;
        var first = true;
        var i = 0;
        while (i < text.Length) {
            if (!char.IsLetter(text[i])) {
                // A word starts after a space, a hyphen, an opening bracket or a slash, never after an apostrophe.
                start = text[i] is ' ' or '-' or '(' or '/' or '"' or '“' or '\n';
                result.Append(text[i++]);
                continue;
            }
            var end = i;
            while (end < text.Length && char.IsLetter(text[end]))
                end++;
            var word = text.AsSpan(i, end - i);
            var keep = !start || !first && Small.Contains(word.ToString()) || HasUpper(word, 1);
            result.Append(keep ? word : char.ToUpperInvariant(word[0]).ToString() + word[1..].ToString());
            first = false;
            start = false;
            i = end;
        }
        return result.ToString();
    }

    private static bool HasUpper(ReadOnlySpan<char> word, int from) {
        for (var i = from; i < word.Length; i++)
            if (char.IsUpper(word[i]))
                return true;
        return false;
    }
}
