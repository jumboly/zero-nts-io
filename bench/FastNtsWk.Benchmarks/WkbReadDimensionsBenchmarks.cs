using BenchmarkDotNet.Attributes;
using FastNtsWk.Abstractions;
using FastNtsWk.Fast;
using FastNtsWk.Reference;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace FastNtsWk.Benchmarks;

/// <summary>
/// WKB Read で次元 (XY / XYZ / XYM / XYZM) ごとに V4 の packed 直挿入効果を計測する。
/// Kind と Coords は 1 軸ずつに固定し、次元だけを振る。
/// </summary>
[MemoryDiagnoser]
public class WkbReadDimensionsBenchmarks
{
    [Params(Ordinates.XY, Ordinates.XYZ, Ordinates.XYM, Ordinates.XYZM)] public Ordinates Ord;
    [Params(10_000)] public int Coords;
    // Why: include BE so the SIMD byte-swap path's per-dimension efficiency is numerically
    // visible — XYZM emits in 32-byte blocks which is the sweet spot for Vector128.Shuffle.
    [Params(ByteOrder.LittleEndian, ByteOrder.BigEndian)] public ByteOrder Order;

    private byte[] _wkb = null!;
    private IWkbReader _nts = null!;
    private IWkbReader _v1 = null!;
    private IWkbReader _v4 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _wkb = FixtureSource.BuildWkb("LineString", Coords, seed: 42, Order, Ord);
        _nts = new NtsWkbReader(FixtureSource.Services);
        _v1 = new FastWkbReaderV1(FixtureSource.Services);
        _v4 = new FastWkbReaderV4(FixtureSource.Services);
    }

    [Benchmark(Baseline = true)] public Geometry Nts() => _nts.Read(_wkb);
    [Benchmark] public Geometry V1_Span() => _v1.Read(_wkb);
    [Benchmark] public Geometry V4_PackedSimd() => _v4.Read(_wkb);
}
