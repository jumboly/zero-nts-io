using FastNtsWk.Abstractions;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace FastNtsWk.Reference;

public sealed class NtsWktWriter : IWktWriter
{
    private readonly WKTWriter _inner;

    public NtsWktWriter(int outputDimension = 4)
    {
        _inner = new WKTWriter(outputDimension);
    }

    public string Write(Geometry geometry) => _inner.Write(geometry);
}
