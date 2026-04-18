using FastNtsWk.Abstractions;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace FastNtsWk.Reference;

public sealed class NtsWkbReader : IWkbReader
{
    private readonly WKBReader _inner;

    public NtsWkbReader(NtsGeometryServices? services = null)
    {
        _inner = new WKBReader(services ?? NtsServicesFactory.CreatePacked());
    }

    public Geometry Read(ReadOnlySpan<byte> wkb) => _inner.Read(wkb.ToArray());

    public Geometry Read(byte[] wkb) => _inner.Read(wkb);
}
