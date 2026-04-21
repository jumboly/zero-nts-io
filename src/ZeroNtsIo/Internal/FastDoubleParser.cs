using System.Globalization;
using System.Runtime.CompilerServices;

namespace ZeroNtsIo.Internal;

/// <summary>
/// 文法 <c>[+-]?\d+(\.\d+)?([eE][+-]?\d+)?</c> をカバーする軽量 ASCII double パーサ。
/// 高速経路の範囲外（mantissa オーバーフロー、極端に大きい指数、特殊値など）は
/// <see cref="double.TryParse(ReadOnlySpan{char}, NumberStyles, IFormatProvider, out double)"/> にフォールバックする。
/// </summary>
internal static class FastDoubleParser
{
    // Why: 10^0..10^22 は IEEE-754 double で厳密表現できる。22 を超えると丸めが発生し
    // 素朴な (mantissa * pow10[e]) 経路は誤差が累積するため、BCL パーサに委譲する。
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
        // Why: 53 ビット mantissa は 2^53 ≈ 9.0e15 まで保持できる。±22 の厳密な 10 冪と組み合わせれば
        // 丸めは 1 回のみで済み、BCL パーサとの差は 1 ULP 以内に収まる。
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
