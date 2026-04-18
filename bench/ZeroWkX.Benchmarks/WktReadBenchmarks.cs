using BenchmarkDotNet.Attributes;
using NetTopologySuite.IO;
using ZeroWkX.Stages;
using ZeroWkX.Naive;
using ZeroWkX.Reference;
using NetTopologySuite.Geometries;

namespace ZeroWkX.Benchmarks;

[MemoryDiagnoser]
public class WktReadBenchmarks
{
    [Params(10, 1_000, 100_000)] public int Coords;
    [Params("LineString", "Polygon", "PolygonWithHoles", "MultiPolygon", "GeometryCollection")] public string Kind = "";

    private string _wkt = "";
    private NtsWktReader _nts = null!;
    private NaiveWktReader _naive = null!;
    private ZWktReaderV1 _v1 = null!;
    private ZWktReaderV2 _v2 = null!;
    private ZWktReaderV3 _v3 = null!;
    private ZWktReader _fast = null!;

    [GlobalSetup]
    public void Setup()
    {
        _wkt = FixtureSource.BuildWkt(Kind, Coords, seed: 42);
        _nts = new NtsWktReader(FixtureSource.Services);
        _naive = new NaiveWktReader(FixtureSource.Services);
        _v1 = new ZWktReaderV1(FixtureSource.Services);
        _v2 = new ZWktReaderV2(FixtureSource.Services);
        _v3 = new ZWktReaderV3(FixtureSource.Services);
        _fast = new ZWktReader(FixtureSource.Services);
    }

    [Benchmark(Baseline = true)] public Geometry Nts() => _nts.Read(_wkt);
    [Benchmark] public Geometry Naive() => _naive.Read(_wkt);
    [Benchmark] public Geometry V1_Span() => _v1.Read(_wkt);
    [Benchmark] public Geometry V2_CustomParser() => _v2.Read(_wkt);
    [Benchmark] public Geometry V3_ArrayPool() => _v3.Read(_wkt);
    [Benchmark] public Geometry Fast() => _fast.Read(_wkt);
}
