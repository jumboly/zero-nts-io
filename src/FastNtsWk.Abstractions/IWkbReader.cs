using NetTopologySuite.Geometries;

namespace FastNtsWk.Abstractions;

public interface IWkbReader
{
    Geometry Read(ReadOnlySpan<byte> wkb);
    Geometry Read(byte[] wkb) => Read(wkb.AsSpan());
}
