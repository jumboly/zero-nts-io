using System.Runtime.CompilerServices;

namespace NetTopologySuite.IO.ZeroWkX.Internal;

internal ref struct WktCursor
{
    public ReadOnlySpan<char> Source;
    public int Pos;

    public WktCursor(ReadOnlySpan<char> source) { Source = source; Pos = 0; }

    public bool AtEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Pos >= Source.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SkipWhitespace()
    {
        var s = Source;
        int p = Pos;
        while (p < s.Length)
        {
            char c = s[p];
            if (c != ' ' && c != '\t' && c != '\r' && c != '\n') break;
            p++;
        }
        Pos = p;
    }

    public void Expect(char c)
    {
        SkipWhitespace();
        if (Pos >= Source.Length || Source[Pos] != c)
            throw new FormatException($"Expected '{c}' at pos {Pos}");
        Pos++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryConsume(char c)
    {
        SkipWhitespace();
        if (Pos < Source.Length && Source[Pos] == c) { Pos++; return true; }
        return false;
    }

    public bool TryConsumeKeyword(ReadOnlySpan<char> word)
    {
        SkipWhitespace();
        if (Pos + word.Length > Source.Length) return false;
        for (int i = 0; i < word.Length; i++)
        {
            char a = Source[Pos + i];
            if (a >= 'a' && a <= 'z') a = (char)(a - 32);
            if (a != word[i]) return false;
        }
        int next = Pos + word.Length;
        if (next < Source.Length && IsWordChar(Source[next])) return false;
        Pos = next;
        return true;
    }

    public ReadOnlySpan<char> ReadWord()
    {
        SkipWhitespace();
        int start = Pos;
        while (Pos < Source.Length && IsLetter(Source[Pos])) Pos++;
        return Source.Slice(start, Pos - start);
    }

    public ReadOnlySpan<char> PeekWord()
    {
        int saved = Pos;
        var w = ReadWord();
        Pos = saved;
        return w;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLetter(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWordChar(char c) => IsLetter(c) || (c >= '0' && c <= '9') || c == '_';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<char> ReadNumberToken()
    {
        SkipWhitespace();
        int start = Pos;
        var s = Source;
        int p = start;
        if (p < s.Length && (s[p] == '+' || s[p] == '-')) p++;
        // Why: NTS writes NaN and Infinity values as "NaN" / "Infinity" in WKT (common for M
        // coordinates and EMPTY POINTs). Capture the literal so the parser can handle it.
        if (p < s.Length && IsLetter(s[p]))
        {
            while (p < s.Length && IsLetter(s[p])) p++;
            Pos = p;
            return s.Slice(start, p - start);
        }
        while (p < s.Length)
        {
            char c = s[p];
            if ((c >= '0' && c <= '9') || c == '.') { p++; continue; }
            if (c == 'e' || c == 'E')
            {
                p++;
                if (p < s.Length && (s[p] == '+' || s[p] == '-')) p++;
                continue;
            }
            break;
        }
        Pos = p;
        return s.Slice(start, p - start);
    }
}
