using NetTopologySuite.Geometries;

namespace FastNtsWk.Abstractions;

public interface IWktReader
{
    Geometry Read(ReadOnlySpan<char> wkt);
    Geometry Read(string wkt) => Read(wkt.AsSpan());
}
