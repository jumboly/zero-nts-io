using BenchmarkDotNet.Attributes;
using NetTopologySuite.IO;
using ZeroWkX.Stages;
using ZeroWkX.Naive;
using ZeroWkX.Reference;
using NetTopologySuite.Geometries;

namespace ZeroWkX.Benchmarks;

[MemoryDiagnoser]
public class WkbReadBenchmarks
{
    [Params(10, 1_000, 100_000)] public int Coords;
    [Params("LineString", "Polygon", "PolygonWithHoles", "MultiPolygon", "GeometryCollection")] public string Kind = "";
    [Params(ByteOrder.LittleEndian, ByteOrder.BigEndian)] public ByteOrder Order;

    private byte[] _wkb = null!;
    private NtsWkbReader _nts = null!;
    private NaiveWkbReader _naive = null!;
    private ZWkbReaderV1 _v1 = null!;
    private ZWkbReader _fast = null!;

    [GlobalSetup]
    public void Setup()
    {
        _wkb = FixtureSource.BuildWkb(Kind, Coords, seed: 42, Order);
        _nts = new NtsWkbReader(FixtureSource.Services);
        _naive = new NaiveWkbReader(FixtureSource.Services);
        _v1 = new ZWkbReaderV1(FixtureSource.Services);
        _fast = new ZWkbReader(FixtureSource.Services);
    }

    [Benchmark(Baseline = true)] public Geometry Nts() => _nts.Read(_wkb);
    [Benchmark] public Geometry Naive() => _naive.Read(_wkb);
    [Benchmark] public Geometry V1_Span() => _v1.Read(_wkb);
    [Benchmark] public Geometry Fast() => _fast.Read(_wkb);
}
