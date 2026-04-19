namespace ZeroNtsIo.Internal;

/// <summary>
/// PostGIS EWKB type-code high-bit flags, orthogonal to the OGC ISO 1000/2000/3000 offsets.
/// Reader accepts all three; Writer only emits <see cref="Srid"/>.
/// </summary>
internal static class EwkbFlags
{
    public const uint Srid = 0x20000000u;
    public const uint M = 0x40000000u;
    public const uint Z = 0x80000000u;
    public const uint Any = 0xE0000000u;
    public const uint OgcMask = 0x1FFFFFFFu;
}
