using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace ZeroNtsIo.Reference;

public sealed class NtsWktWriter
{
    private readonly WKTWriter _inner;

    public NtsWktWriter(int outputDimension = 4)
    {
        _inner = new WKTWriter(outputDimension);
    }

    public string Write(Geometry geometry) => _inner.Write(geometry);
}
