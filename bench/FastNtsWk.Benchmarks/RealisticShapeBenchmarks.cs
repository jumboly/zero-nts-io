using BenchmarkDotNet.Attributes;
using FastNtsWk.Abstractions;
using FastNtsWk.Fast;
using FastNtsWk.Naive;
using FastNtsWk.Reference;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace FastNtsWk.Benchmarks;

/// <summary>
/// 決定論的に生成した「海岸線的」な MultiPolygon（総約 7,000 座標、密な準共線点＋複数穴＋サイズ多様なリング）
/// を一様乱数フィクスチャと別軸で計測する。GPS/地物データ的な形状特性が uniform random と異なる挙動を
/// 引き起こすかを確認する用途。
/// </summary>
[MemoryDiagnoser]
public class RealisticShapeBenchmarks
{
    [Params(ByteOrder.LittleEndian, ByteOrder.BigEndian)] public ByteOrder Order;

    private byte[] _wkb = null!;
    private string _wkt = "";
    private IWkbReader _ntsWkb = null!;
    private IWkbReader _naiveWkb = null!;
    private IWkbReader _v1Wkb = null!;
    private IWkbReader _v4Wkb = null!;
    private IWktReader _ntsWkt = null!;
    private IWktReader _naiveWkt = null!;
    private IWktReader _v4Wkt = null!;

    [GlobalSetup]
    public void Setup()
    {
        _wkb = FixtureSource.BuildRealisticWkb(Order, seed: 42);
        _wkt = FixtureSource.BuildRealisticWkt(seed: 42);
        _ntsWkb = new NtsWkbReader(FixtureSource.Services);
        _naiveWkb = new NaiveWkbReader(FixtureSource.Services);
        _v1Wkb = new FastWkbReaderV1(FixtureSource.Services);
        _v4Wkb = new FastWkbReaderV4(FixtureSource.Services);
        _ntsWkt = new NtsWktReader(FixtureSource.Services);
        _naiveWkt = new NaiveWktReader(FixtureSource.Services);
        _v4Wkt = new FastWktReaderV4(FixtureSource.Services);
    }

    [BenchmarkCategory("Wkb"), Benchmark(Baseline = true)] public Geometry Wkb_Nts() => _ntsWkb.Read(_wkb);
    [BenchmarkCategory("Wkb"), Benchmark] public Geometry Wkb_Naive() => _naiveWkb.Read(_wkb);
    [BenchmarkCategory("Wkb"), Benchmark] public Geometry Wkb_V1() => _v1Wkb.Read(_wkb);
    [BenchmarkCategory("Wkb"), Benchmark] public Geometry Wkb_V4() => _v4Wkb.Read(_wkb);

    [BenchmarkCategory("Wkt"), Benchmark] public Geometry Wkt_Nts() => _ntsWkt.Read(_wkt);
    [BenchmarkCategory("Wkt"), Benchmark] public Geometry Wkt_Naive() => _naiveWkt.Read(_wkt);
    [BenchmarkCategory("Wkt"), Benchmark] public Geometry Wkt_V4() => _v4Wkt.Read(_wkt);
}
