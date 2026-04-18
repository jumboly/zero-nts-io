using System.Globalization;
using System.Runtime.CompilerServices;

namespace FastNtsWk.Fast.Internal;

/// <summary>
/// Lightweight ASCII double parser covering the grammar <c>[+-]?\d+(\.\d+)?([eE][+-]?\d+)?</c>.
/// Falls back to <see cref="double.TryParse(ReadOnlySpan{char}, NumberStyles, IFormatProvider, out double)"/>
/// for inputs outside the fast path (overflowing mantissa, very large exponents, special values).
/// </summary>
internal static class FastDoubleParser
{
    // Why: 10^0..10^22 are represented exactly in IEEE-754 double. Beyond 22 rounding kicks in
    // and the naive (mantissa * pow10[e]) path drifts, so we delegate to the BCL parser.
    private static readonly double[] Pow10 =
    [
        1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11,
        1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22,
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Parse(ReadOnlySpan<char> s)
    {
        if (!TryParse(s, out var v)) throw new FormatException($"Bad number: '{s.ToString()}'");
        return v;
    }

    public static bool TryParse(ReadOnlySpan<char> s, out double result)
    {
        int i = 0;
        int len = s.Length;
        if (len == 0) { result = 0; return false; }

        bool neg = false;
        if (s[i] == '+') i++;
        else if (s[i] == '-') { neg = true; i++; }

        ulong mantissa = 0;
        int mantissaDigits = 0;
        int fracDigits = 0;
        bool afterDot = false;
        bool overflow = false;

        while (i < len)
        {
            char c = s[i];
            if ((uint)(c - '0') <= 9u)
            {
                if (mantissa <= (ulong.MaxValue - 9) / 10)
                {
                    mantissa = mantissa * 10 + (uint)(c - '0');
                    mantissaDigits++;
                }
                else
                {
                    overflow = true;
                }
                if (afterDot) fracDigits++;
                i++;
            }
            else if (c == '.' && !afterDot)
            {
                afterDot = true;
                i++;
            }
            else break;
        }

        if (mantissaDigits == 0 || overflow) return Fallback(s, out result);

        int explicitExp = 0;
        if (i < len && (s[i] == 'e' || s[i] == 'E'))
        {
            i++;
            bool expNeg = false;
            if (i < len && s[i] == '+') i++;
            else if (i < len && s[i] == '-') { expNeg = true; i++; }
            int expDigits = 0;
            while (i < len && (uint)(s[i] - '0') <= 9u)
            {
                explicitExp = explicitExp * 10 + (s[i] - '0');
                expDigits++;
                i++;
            }
            if (expDigits == 0) return Fallback(s, out result);
            if (expNeg) explicitExp = -explicitExp;
        }

        if (i != len) return Fallback(s, out result);

        int exp = explicitExp - fracDigits;
        // Why: the 53-bit mantissa can hold up to 2^53 ≈ 9.0e15; combined with a ±22 exact
        // power of ten this keeps one rounding so the result is within 1 ULP of the BCL parser.
        if (mantissa > (1UL << 53) || exp > 22 || exp < -22)
            return Fallback(s, out result);

        double value = mantissa;
        if (exp >= 0) value *= Pow10[exp];
        else value /= Pow10[-exp];

        result = neg ? -value : value;
        return true;
    }

    private static bool Fallback(ReadOnlySpan<char> s, out double result) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
}
