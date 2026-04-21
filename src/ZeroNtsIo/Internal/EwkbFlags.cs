namespace ZeroNtsIo.Internal;

/// <summary>
/// PostGIS EWKB タイプコードの高位ビットフラグ。OGC ISO の 1000/2000/3000 オフセットとは直交する。
/// Reader は 3 種すべて受理するが、Writer は <see cref="Srid"/> のみ出力する。
/// </summary>
internal static class EwkbFlags
{
    public const uint Srid = 0x20000000u;
    public const uint M = 0x40000000u;
    public const uint Z = 0x80000000u;
    public const uint Any = 0xE0000000u;
    public const uint OgcMask = 0x1FFFFFFFu;
}
