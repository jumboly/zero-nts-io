using BenchmarkDotNet.Attributes;
using NetTopologySuite.IO;
using ZeroNtsIo.Stages;
using ZeroNtsIo.Naive;
using ZeroNtsIo.Reference;
using NetTopologySuite.Geometries;

namespace ZeroNtsIo.Benchmarks;

[MemoryDiagnoser]
public class WktReadBenchmarks
{
    [Params(1, 10, 100, 1_000, 10_000)] public int Coords;
    [Params("Point", "MultiPoint", "LineString", "Polygon", "PolygonWithHoles", "MultiPolygon", "GeometryCollection")] public string Kind = "";

    private string _wkt = "";
    private NtsWktReader _nts = null!;
    private WKTReader _ntsDefault = null!;
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
        // Why: NTS の既定 services (= CoordinateArraySequenceFactory) を使う比較軸。
        // 既定 factory は Z/M を黙って落とすので、XY 固定のこのベンチにしか足さない。
        _ntsDefault = new WKTReader();
        _naive = new NaiveWktReader(FixtureSource.Services);
        _v1 = new ZWktReaderV1(FixtureSource.Services);
        _v2 = new ZWktReaderV2(FixtureSource.Services);
        _v3 = new ZWktReaderV3(FixtureSource.Services);
        _fast = new ZWktReader(FixtureSource.Services);
    }

    [Benchmark(Baseline = true)] public Geometry Nts() => _nts.Read(_wkt);
    [Benchmark] public Geometry Nts_Default() => _ntsDefault.Read(_wkt);
    [Benchmark] public Geometry Naive() => _naive.Read(_wkt);
    [Benchmark] public Geometry V1_Span() => _v1.Read(_wkt);
    [Benchmark] public Geometry V2_CustomParser() => _v2.Read(_wkt);
    [Benchmark] public Geometry V3_ArrayPool() => _v3.Read(_wkt);
    [Benchmark] public Geometry Fast() => _fast.Read(_wkt);
}
