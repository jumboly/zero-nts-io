using BenchmarkDotNet.Attributes;
using NetTopologySuite.IO;
using ZeroNtsIo.Stages;
using ZeroNtsIo.Reference;
using NetTopologySuite.Geometries;

namespace ZeroNtsIo.Benchmarks;

/// <summary>
/// WKB Read で次元 (XY / XYZ / XYM / XYZM) ごとに Fast の packed 直挿入効果を計測する。
/// Kind と Coords は 1 軸ずつに固定し、次元だけを振る。
/// </summary>
[MemoryDiagnoser]
public class WkbReadDimensionsBenchmarks
{
    [Params(Ordinates.XY, Ordinates.XYZ, Ordinates.XYM, Ordinates.XYZM)] public Ordinates Ord;
    [Params(10_000)] public int Coords;
    // Why: BE も含めて、SIMD バイトスワップ経路の次元毎の効率を数値として可視化する。
    // XYZM は 32 バイト単位での出力となり、これが Vector128.Shuffle の最も効率が良いサイズ。
    [Params(ByteOrder.LittleEndian, ByteOrder.BigEndian)] public ByteOrder Order;

    private byte[] _wkb = null!;
    private NtsWkbReader _nts = null!;
    private ZWkbReaderV1 _v1 = null!;
    private ZWkbReader _fast = null!;

    [GlobalSetup]
    public void Setup()
    {
        _wkb = FixtureSource.BuildWkb("LineString", Coords, seed: 42, Order, Ord);
        _nts = new NtsWkbReader(FixtureSource.Services);
        _v1 = new ZWkbReaderV1(FixtureSource.Services);
        _fast = new ZWkbReader(FixtureSource.Services);
    }

    [Benchmark(Baseline = true)] public Geometry Nts() => _nts.Read(_wkb);
    [Benchmark] public Geometry V1_Span() => _v1.Read(_wkb);
    [Benchmark] public Geometry Fast() => _fast.Read(_wkb);
}
