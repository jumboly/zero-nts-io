using ZeroWkX.Naive;
using NetTopologySuite.IO.ZeroWkX;
using ZeroWkX.Reference;
using ZeroWkX.Tests.Fixtures;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Xunit;

namespace ZeroWkX.Tests;

/// <summary>
/// Full N-by-N matrix: every writer's output must be readable by every reader, producing a
/// geometry bit-equal to the original. Catches regressions where one writer's output becomes
/// incompatible with another implementation's reader (the realistic deployment concern).
/// </summary>
public class WriterInteropTests
{
    private static readonly (string Name, Func<Geometry, string> Write)[] WktWriters =
    [
        ("Nts",   new NtsWktWriter().Write),
        ("Naive", new NaiveWktWriter().Write),
        ("Fast",  new ZWktWriter().Write),
    ];

    private static readonly (string Name, Func<Geometry, ByteOrder, byte[]> Write)[] WkbWriters =
    [
        ("Nts",   new NtsWkbWriter().Write),
        ("Naive", new NaiveWkbWriter().Write),
        ("Fast",  new ZWkbWriter().Write),
    ];

    private static readonly (string Name, Func<string, Geometry> Read, long Ulp)[] WktReaders =
    [
        ("Nts",   new NtsWktReader(Samples.Services).Read, 0),
        ("Naive", new NaiveWktReader(Samples.Services).Read, 0),
        ("V1",    new Stages.ZWktReaderV1(Samples.Services).Read, 0),
        ("V2",    new Stages.ZWktReaderV2(Samples.Services).Read, 1),
        ("V3",    new Stages.ZWktReaderV3(Samples.Services).Read, 1),
        ("Fast",  new ZWktReader(Samples.Services).Read, 1),
    ];

    private static readonly (string Name, Func<byte[], Geometry> Read)[] WkbReaders =
    [
        ("Nts",   new NtsWkbReader(Samples.Services).Read),
        ("Naive", new NaiveWkbReader(Samples.Services).Read),
        ("V1",    new Stages.ZWkbReaderV1(Samples.Services).Read),
        ("Fast",  new ZWkbReader(Samples.Services).Read),
    ];

    [Theory]
    [MemberData(nameof(GeometryGenerator.CombinationMatrix), MemberType = typeof(GeometryGenerator))]
    public void Every_wkt_writer_output_readable_by_every_reader(string kind, Ordinates ord, int coords, int seed)
    {
        var g = GeometryGenerator.Build(kind, ord, coords, seed);

        foreach (var (wname, write) in WktWriters)
        {
            var text = write(g);
            foreach (var (rname, read, ulp) in WktReaders)
            {
                Geometry parsed;
                try { parsed = read(text); }
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

        foreach (var (wname, write) in WkbWriters)
        foreach (var order in new[] { ByteOrder.LittleEndian, ByteOrder.BigEndian })
        {
            byte[] bytes;
            try { bytes = write(g, order); }
            catch (Exception ex)
            {
                throw new Xunit.Sdk.XunitException(
                    $"{wname} WKB writer threw on {kind}/{ord}/coords={coords}/{order}: {ex.Message}");
            }

            foreach (var (rname, read) in WkbReaders)
            {
                Geometry parsed;
                try { parsed = read(bytes); }
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
