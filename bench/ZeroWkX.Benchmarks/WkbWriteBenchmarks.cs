using BenchmarkDotNet.Attributes;
using NetTopologySuite.IO;
using ZeroWkX.Naive;
using ZeroWkX.Reference;
using NetTopologySuite.Geometries;

namespace ZeroWkX.Benchmarks;

[MemoryDiagnoser]
public class WkbWriteBenchmarks
{
    [Params(10, 1_000, 100_000)] public int Coords;
    [Params("LineString", "Polygon", "PolygonWithHoles", "MultiPolygon", "GeometryCollection")] public string Kind = "";
    [Params(ByteOrder.LittleEndian, ByteOrder.BigEndian)] public ByteOrder Order;

    private Geometry _geom = null!;
    private NtsWkbWriter _nts = null!;
    private NaiveWkbWriter _naive = null!;
    private ZWkbWriter _fast = null!;

    [GlobalSetup]
    public void Setup()
    {
        _geom = FixtureSource.BuildGeometry(Kind, Coords, seed: 42);
        _nts = new NtsWkbWriter();
        _naive = new NaiveWkbWriter();
        _fast = new ZWkbWriter();
    }

    [Benchmark(Baseline = true)] public byte[] Nts() => _nts.Write(_geom, Order);
    [Benchmark] public byte[] Naive() => _naive.Write(_geom, Order);
    [Benchmark] public byte[] Fast() => _fast.Write(_geom, Order);
}
