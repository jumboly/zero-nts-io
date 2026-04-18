using FastNtsWk.Abstractions;
using FastNtsWk.Fast;
using FastNtsWk.Naive;
using FastNtsWk.Reference;
using FastNtsWk.Tests.Fixtures;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Xunit;

namespace FastNtsWk.Tests;

/// <summary>
/// Full N-by-N matrix: every writer's output must be readable by every reader, producing a
/// geometry bit-equal to the original. Catches regressions where one writer's output becomes
/// incompatible with another implementation's reader (the realistic deployment concern).
/// </summary>
public class WriterInteropTests
{
    private static readonly (string Name, IWktWriter W)[] WktWriters =
    [
        ("Nts", new NtsWktWriter()),
        ("Naive", new NaiveWktWriter()),
        ("Fast", new FastWktWriter()),
    ];

    private static readonly (string Name, IWkbWriter W)[] WkbWriters =
    [
        ("Nts", new NtsWkbWriter()),
        ("Naive", new NaiveWkbWriter()),
        ("Fast", new FastWkbWriter()),
    ];

    private static readonly (string Name, IWktReader R, long Ulp)[] WktReaders =
    [
        ("Nts", new NtsWktReader(Samples.Services), 0),
        ("Naive", new NaiveWktReader(Samples.Services), 0),
        ("V1", new FastWktReaderV1(Samples.Services), 0),
        ("V2", new FastWktReaderV2(Samples.Services), 1),
        ("V3", new FastWktReaderV3(Samples.Services), 1),
        ("V4", new FastWktReaderV4(Samples.Services), 1),
    ];

    private static readonly (string Name, IWkbReader R)[] WkbReaders =
    [
        ("Nts", new NtsWkbReader(Samples.Services)),
        ("Naive", new NaiveWkbReader(Samples.Services)),
        ("V1", new FastWkbReaderV1(Samples.Services)),
        ("V4", new FastWkbReaderV4(Samples.Services)),
    ];

    [Theory]
    [MemberData(nameof(GeometryGenerator.CombinationMatrix), MemberType = typeof(GeometryGenerator))]
    public void Every_wkt_writer_output_readable_by_every_reader(string kind, Ordinates ord, int coords, int seed)
    {
        var g = GeometryGenerator.Build(kind, ord, coords, seed);

        foreach (var (wname, w) in WktWriters)
        {
            var text = w.Write(g);
            foreach (var (rname, r, ulp) in WktReaders)
            {
                Geometry parsed;
                try { parsed = r.Read(text); }
                catch (Exception ex)
                {
                    throw new Xunit.Sdk.XunitException(
                        $"{wname}→{rname} WKT interop broken on {kind}/{ord}/coords={coords}: {ex.Message}");
                }

                try { CoordinateAsserts.AssertCoordinatesBitEqual(g, parsed, ulp); }
                catch (Xunit.Sdk.XunitException)
                {
                    throw new Xunit.Sdk.XunitException(
                        $"{wname}→{rname} WKT roundtrip diverged on {kind}/{ord}/coords={coords}");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GeometryGenerator.CombinationMatrix), MemberType = typeof(GeometryGenerator))]
    public void Every_wkb_writer_output_readable_by_every_reader(string kind, Ordinates ord, int coords, int seed)
    {
        var g = GeometryGenerator.Build(kind, ord, coords, seed);

        foreach (var (wname, w) in WkbWriters)
        foreach (var order in new[] { ByteOrder.LittleEndian, ByteOrder.BigEndian })
        {
            byte[] bytes;
            try { bytes = w.Write(g, order); }
            catch (Exception ex)
            {
                throw new Xunit.Sdk.XunitException(
                    $"{wname} WKB writer threw on {kind}/{ord}/coords={coords}/{order}: {ex.Message}");
            }

            foreach (var (rname, r) in WkbReaders)
            {
                Geometry parsed;
                try { parsed = r.Read(bytes); }
                catch (Exception ex)
                {
                    throw new Xunit.Sdk.XunitException(
                        $"{wname}→{rname} WKB interop broken on {kind}/{ord}/coords={coords}/{order}: {ex.Message}");
                }

                try { CoordinateAsserts.AssertCoordinatesBitEqual(g, parsed); }
                catch (Xunit.Sdk.XunitException)
                {
                    throw new Xunit.Sdk.XunitException(
                        $"{wname}→{rname} WKB roundtrip diverged on {kind}/{ord}/coords={coords}/{order}");
                }
            }
        }
    }
}
