using BenchmarkDotNet.Attributes;
using FastNtsWk.Abstractions;
using FastNtsWk.Fast;
using FastNtsWk.Reference;
using NetTopologySuite.Geometries;

namespace FastNtsWk.Benchmarks;

/// <summary>
/// WKT Read で次元 (XY / XYZ / XYM / XYZM) ごとに V4 の packed 直挿入効果を計測する。
/// </summary>
[MemoryDiagnoser]
public class WktReadDimensionsBenchmarks
{
    [Params(Ordinates.XY, Ordinates.XYZ, Ordinates.XYM, Ordinates.XYZM)] public Ordinates Ord;
    [Params(10_000)] public int Coords;

    private string _wkt = "";
    private IWktReader _nts = null!;
    private IWktReader _v1 = null!;
    private IWktReader _v4 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _wkt = FixtureSource.BuildWkt("LineString", Coords, seed: 42, Ord);
        _nts = new NtsWktReader(FixtureSource.Services);
        _v1 = new FastWktReaderV1(FixtureSource.Services);
        _v4 = new FastWktReaderV4(FixtureSource.Services);
    }

    [Benchmark(Baseline = true)] public Geometry Nts() => _nts.Read(_wkt);
    [Benchmark] public Geometry V1_Span() => _v1.Read(_wkt);
    [Benchmark] public Geometry V4_Packed() => _v4.Read(_wkt);
}
