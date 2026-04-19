using BenchmarkDotNet.Attributes;
using NetTopologySuite.IO;
using ZeroNtsIo.Naive;
using ZeroNtsIo.Reference;
using NetTopologySuite.Geometries;

namespace ZeroNtsIo.Benchmarks;

[MemoryDiagnoser]
public class WkbWriteBenchmarks
{
    [Params(1, 10, 100, 1_000, 10_000)] public int Coords;
    [Params("Point", "MultiPoint", "LineString", "Polygon", "PolygonWithHoles", "MultiPolygon", "GeometryCollection")] public string Kind = "";
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
