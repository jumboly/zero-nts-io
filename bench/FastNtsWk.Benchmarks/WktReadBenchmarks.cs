using BenchmarkDotNet.Attributes;
using FastNtsWk.Abstractions;
using FastNtsWk.Fast;
using FastNtsWk.Naive;
using FastNtsWk.Reference;
using NetTopologySuite.Geometries;

namespace FastNtsWk.Benchmarks;

[MemoryDiagnoser]
public class WktReadBenchmarks
{
    [Params(10, 1_000, 100_000)] public int Coords;
    [Params("LineString", "Polygon", "PolygonWithHoles", "MultiPolygon", "GeometryCollection")] public string Kind = "";

    private string _wkt = "";
    private IWktReader _nts = null!;
    private IWktReader _naive = null!;
    private IWktReader _v1 = null!;
    private IWktReader _v2 = null!;
    private IWktReader _v3 = null!;
    private IWktReader _v4 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _wkt = FixtureSource.BuildWkt(Kind, Coords, seed: 42);
        _nts = new NtsWktReader(FixtureSource.Services);
        _naive = new NaiveWktReader(FixtureSource.Services);
        _v1 = new FastWktReaderV1(FixtureSource.Services);
        _v2 = new FastWktReaderV2(FixtureSource.Services);
        _v3 = new FastWktReaderV3(FixtureSource.Services);
        _v4 = new FastWktReaderV4(FixtureSource.Services);
    }

    [Benchmark(Baseline = true)] public Geometry Nts() => _nts.Read(_wkt);
    [Benchmark] public Geometry Naive() => _naive.Read(_wkt);
    [Benchmark] public Geometry V1_Span() => _v1.Read(_wkt);
    [Benchmark] public Geometry V2_CustomParser() => _v2.Read(_wkt);
    [Benchmark] public Geometry V3_ArrayPool() => _v3.Read(_wkt);
    [Benchmark] public Geometry V4_Packed() => _v4.Read(_wkt);
}
