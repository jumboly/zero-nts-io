using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace ZeroNtsIo.Reference;

public sealed class NtsWktReader
{
    private readonly WKTReader _inner;

    public NtsWktReader(NtsGeometryServices? services = null)
    {
        _inner = new WKTReader(services ?? NtsServicesFactory.CreatePacked());
    }

    public Geometry Read(ReadOnlySpan<char> wkt) => _inner.Read(wkt.ToString());

    public Geometry Read(string wkt) => _inner.Read(wkt);
}
