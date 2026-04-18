using BenchmarkDotNet.Attributes;
using NetTopologySuite.IO;
using ZeroWkX.Naive;
using ZeroWkX.Reference;
using NetTopologySuite.Geometries;

namespace ZeroWkX.Benchmarks;

[MemoryDiagnoser]
public class WktWriteBenchmarks
{
    [Params(10, 1_000, 100_000)] public int Coords;
    [Params("LineString", "Polygon", "PolygonWithHoles", "MultiPolygon", "GeometryCollection")] public string Kind = "";

    private Geometry _geom = null!;
    private NtsWktWriter _nts = null!;
    private NaiveWktWriter _naive = null!;
    private ZWktWriter _fast = null!;

    [GlobalSetup]
    public void Setup()
    {
        _geom = FixtureSource.BuildGeometry(Kind, Coords, seed: 42);
        _nts = new NtsWktWriter();
        _naive = new NaiveWktWriter();
        _fast = new ZWktWriter();
    }

    [Benchmark(Baseline = true)] public string Nts() => _nts.Write(_geom);
    [Benchmark] public string Naive() => _naive.Write(_geom);
    [Benchmark] public string Fast() => _fast.Write(_geom);
}
