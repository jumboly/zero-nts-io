using BenchmarkDotNet.Attributes;
using NetTopologySuite.IO;
using ZeroWkX.Stages;
using ZeroWkX.Naive;
using ZeroWkX.Reference;
using NetTopologySuite.Geometries;

namespace ZeroWkX.Benchmarks;

/// <summary>
/// 実データベンチ: 国土数値情報 行政区域データ N03-20240101 香川県本島ポリゴン（22,418 座標）。
/// 詳細は bench/Data/README.md 参照。
/// </summary>
[MemoryDiagnoser]
public class NaturalDataBenchmarks
{
    private static readonly string WkbPath = LocateData("N03_Kagawa.wkb");
    private static readonly string WktPath = LocateData("N03_Kagawa.wkt.txt");

    [Params(ByteOrder.LittleEndian, ByteOrder.BigEndian)] public ByteOrder Order;

    private byte[] _wkb = null!;
    private string _wkt = "";
    private NtsWkbReader _ntsWkb = null!;
    private NaiveWkbReader _naiveWkb = null!;
    private ZWkbReaderV1 _v1Wkb = null!;
    private ZWkbReader _fastWkb = null!;
    private NtsWktReader _ntsWkt = null!;
    private NaiveWktReader _naiveWkt = null!;
    private ZWktReader _fastWkt = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Why: the committed LE file is the canonical form; rewrite it to BE once at setup when
        // benchmarking the BE path. We measure parsing, not re-encoding.
        var baseWkb = File.ReadAllBytes(WkbPath);
        if (Order == ByteOrder.BigEndian)
        {
            var geom = new WKBReader(FixtureSource.Services).Read(baseWkb);
            _wkb = new WKBWriter(ByteOrder.BigEndian, handleSRID: false, emitZ: false, emitM: false).Write(geom);
        }
        else _wkb = baseWkb;

        _wkt = File.ReadAllText(WktPath);
        _ntsWkb = new NtsWkbReader(FixtureSource.Services);
        _naiveWkb = new NaiveWkbReader(FixtureSource.Services);
        _v1Wkb = new ZWkbReaderV1(FixtureSource.Services);
        _fastWkb = new ZWkbReader(FixtureSource.Services);
        _ntsWkt = new NtsWktReader(FixtureSource.Services);
        _naiveWkt = new NaiveWktReader(FixtureSource.Services);
        _fastWkt = new ZWktReader(FixtureSource.Services);
    }

    [BenchmarkCategory("Wkb"), Benchmark(Baseline = true)] public Geometry Wkb_Nts() => _ntsWkb.Read(_wkb);
    [BenchmarkCategory("Wkb"), Benchmark] public Geometry Wkb_Naive() => _naiveWkb.Read(_wkb);
    [BenchmarkCategory("Wkb"), Benchmark] public Geometry Wkb_V1() => _v1Wkb.Read(_wkb);
    [BenchmarkCategory("Wkb"), Benchmark] public Geometry Wkb_Fast() => _fastWkb.Read(_wkb);

    [BenchmarkCategory("Wkt"), Benchmark] public Geometry Wkt_Nts() => _ntsWkt.Read(_wkt);
    [BenchmarkCategory("Wkt"), Benchmark] public Geometry Wkt_Naive() => _naiveWkt.Read(_wkt);
    [BenchmarkCategory("Wkt"), Benchmark] public Geometry Wkt_Fast() => _fastWkt.Read(_wkt);

    private static string LocateData(string fileName)
    {
        // Why: BenchmarkDotNet copies the build into a generated nested folder (bench/.../ShortRun-N/bin/...),
        // so the repo root can be 10+ levels up. Walk until we find the solution file, then anchor.
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 20 && !string.IsNullOrEmpty(dir); i++)
        {
            if (File.Exists(Path.Combine(dir, "ZeroWkX.slnx")) || File.Exists(Path.Combine(dir, "ZeroWkX.sln")))
            {
                var candidate = Path.Combine(dir, "bench", "Data", fileName);
                if (File.Exists(candidate)) return candidate;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException(
            $"Could not locate {fileName}. Expected under <repo>/bench/Data/. Run the scratch converter if missing.");
    }
}
