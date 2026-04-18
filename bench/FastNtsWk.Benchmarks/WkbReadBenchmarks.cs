using BenchmarkDotNet.Attributes;
using FastNtsWk.Abstractions;
using FastNtsWk.Fast;
using FastNtsWk.Naive;
using FastNtsWk.Reference;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace FastNtsWk.Benchmarks;

[MemoryDiagnoser]
public class WkbReadBenchmarks
{
    [Params(10, 1_000, 100_000)] public int Coords;
    [Params("LineString", "Polygon", "PolygonWithHoles", "MultiPolygon", "GeometryCollection")] public string Kind = "";
    [Params(ByteOrder.LittleEndian, ByteOrder.BigEndian)] public ByteOrder Order;

    private byte[] _wkb = null!;
    private IWkbReader _nts = null!;
    private IWkbReader _naive = null!;
    private IWkbReader _v1 = null!;
    private IWkbReader _v4 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _wkb = FixtureSource.BuildWkb(Kind, Coords, seed: 42, Order);
        _nts = new NtsWkbReader(FixtureSource.Services);
        _naive = new NaiveWkbReader(FixtureSource.Services);
        _v1 = new FastWkbReaderV1(FixtureSource.Services);
        _v4 = new FastWkbReaderV4(FixtureSource.Services);
    }

    [Benchmark(Baseline = true)] public Geometry Nts() => _nts.Read(_wkb);
    [Benchmark] public Geometry Naive() => _naive.Read(_wkb);
    [Benchmark] public Geometry V1_Span() => _v1.Read(_wkb);
    [Benchmark] public Geometry V4_PackedSimd() => _v4.Read(_wkb);
}
