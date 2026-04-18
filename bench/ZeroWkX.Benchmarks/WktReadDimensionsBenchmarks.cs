using BenchmarkDotNet.Attributes;
using NetTopologySuite.IO;
using ZeroWkX.Stages;
using ZeroWkX.Reference;
using NetTopologySuite.Geometries;

namespace ZeroWkX.Benchmarks;

/// <summary>
/// WKT Read で次元 (XY / XYZ / XYM / XYZM) ごとに Fast の packed 直挿入効果を計測する。
/// </summary>
[MemoryDiagnoser]
public class WktReadDimensionsBenchmarks
{
    [Params(Ordinates.XY, Ordinates.XYZ, Ordinates.XYM, Ordinates.XYZM)] public Ordinates Ord;
    [Params(10_000)] public int Coords;

    private string _wkt = "";
    private NtsWktReader _nts = null!;
    private ZWktReaderV1 _v1 = null!;
    private ZWktReader _fast = null!;

    [GlobalSetup]
    public void Setup()
    {
        _wkt = FixtureSource.BuildWkt("LineString", Coords, seed: 42, Ord);
        _nts = new NtsWktReader(FixtureSource.Services);
        _v1 = new ZWktReaderV1(FixtureSource.Services);
        _fast = new ZWktReader(FixtureSource.Services);
    }

    [Benchmark(Baseline = true)] public Geometry Nts() => _nts.Read(_wkt);
    [Benchmark] public Geometry V1_Span() => _v1.Read(_wkt);
    [Benchmark] public Geometry Fast() => _fast.Read(_wkt);
}
