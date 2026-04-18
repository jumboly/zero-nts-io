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
/// Reads generated geometries through every implementation and asserts bit-for-bit coordinate
/// equality against the NTS oracle. Covers every geometry type × every dimension × three sizes.
/// </summary>
public class PropertyBasedTests
{
    private static readonly IWktWriter NtsWktW = new NtsWktWriter();
    private static readonly IWkbWriter NtsWkbW = new NtsWkbWriter();

    private static readonly IWktReader NtsWkt = new NtsWktReader(Samples.Services);
    private static readonly IWkbReader NtsWkb = new NtsWkbReader(Samples.Services);

    // WKT readers
    private static readonly IWktReader[] WktReaders =
    [
        new NaiveWktReader(Samples.Services),
        new FastWktReaderV1(Samples.Services),
        new FastWktReaderV2(Samples.Services),
        new FastWktReaderV3(Samples.Services),
        new FastWktReaderV4(Samples.Services),
    ];
    private static readonly string[] WktReaderNames = ["Naive", "V1", "V2", "V3", "V4"];

    // WKB readers (V2/V3 intentionally omitted; they're not implemented for WKB)
    private static readonly IWkbReader[] WkbReaders =
    [
        new NaiveWkbReader(Samples.Services),
        new FastWkbReaderV1(Samples.Services),
        new FastWkbReaderV4(Samples.Services),
    ];
    private static readonly string[] WkbReaderNames = ["Naive", "V1", "V4"];

    // Why: the custom double parser (FastDoubleParser) can round differently from the BCL parser
    // by at most 1 ULP on the fast path. WKB has no text parsing so we require 0 ULP there.
    private static readonly long[] WktUlpTolerance = [0, 0, 1, 1, 1]; // aligned with WktReaders/Names

    [Theory]
    [MemberData(nameof(GeometryGenerator.CombinationMatrix), MemberType = typeof(GeometryGenerator))]
    public void Wkt_read_matches_nts_for_every_impl(string kind, Ordinates ord, int coords, int seed)
    {
        var g = GeometryGenerator.Build(kind, ord, coords, seed);
        var wkt = NtsWktW.Write(g);
        var expected = NtsWkt.Read(wkt);

        for (int i = 0; i < WktReaders.Length; i++)
        {
            var actual = WktReaders[i].Read(wkt);
            try
            {
                CoordinateAsserts.AssertCoordinatesBitEqual(expected, actual, WktUlpTolerance[i]);
            }
            catch (Xunit.Sdk.XunitException)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Reader '{WktReaderNames[i]}' diverged on {kind}/{ord}/coords={coords}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(GeometryGenerator.CombinationMatrix), MemberType = typeof(GeometryGenerator))]
    public void Wkb_read_matches_nts_for_every_impl_le_and_be(string kind, Ordinates ord, int coords, int seed)
    {
        var g = GeometryGenerator.Build(kind, ord, coords, seed);
        var wkbLe = NtsWkbW.Write(g, ByteOrder.LittleEndian);
        var wkbBe = NtsWkbW.Write(g, ByteOrder.BigEndian);
        var expected = NtsWkb.Read(wkbLe);

        for (int i = 0; i < WkbReaders.Length; i++)
        {
            try
            {
                CoordinateAsserts.AssertCoordinatesBitEqual(expected, WkbReaders[i].Read(wkbLe));
                CoordinateAsserts.AssertCoordinatesBitEqual(expected, WkbReaders[i].Read(wkbBe));
            }
            catch (Xunit.Sdk.XunitException)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Reader '{WkbReaderNames[i]}' diverged on {kind}/{ord}/coords={coords}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(GeometryGenerator.CombinationMatrix), MemberType = typeof(GeometryGenerator))]
    public void FastWktWriter_output_roundtrips_through_all_readers(string kind, Ordinates ord, int coords, int seed)
    {
        var g = GeometryGenerator.Build(kind, ord, coords, seed);
        var fastWkt = new FastWktWriter().Write(g);

        for (int i = 0; i < WktReaders.Length; i++)
        {
            var parsed = WktReaders[i].Read(fastWkt);
            try
            {
                CoordinateAsserts.AssertCoordinatesBitEqual(g, parsed, WktUlpTolerance[i]);
            }
            catch (Xunit.Sdk.XunitException)
            {
                throw new Xunit.Sdk.XunitException(
                    $"FastWktWriter output unreadable by '{WktReaderNames[i]}' on {kind}/{ord}/coords={coords}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(GeometryGenerator.CombinationMatrix), MemberType = typeof(GeometryGenerator))]
    public void FastWkbWriter_output_roundtrips_through_all_readers(string kind, Ordinates ord, int coords, int seed)
    {
        var g = GeometryGenerator.Build(kind, ord, coords, seed);
        var fastLe = new FastWkbWriter().Write(g, ByteOrder.LittleEndian);
        var fastBe = new FastWkbWriter().Write(g, ByteOrder.BigEndian);

        for (int i = 0; i < WkbReaders.Length; i++)
        {
            try
            {
                CoordinateAsserts.AssertCoordinatesBitEqual(g, WkbReaders[i].Read(fastLe));
                CoordinateAsserts.AssertCoordinatesBitEqual(g, WkbReaders[i].Read(fastBe));
            }
            catch (Xunit.Sdk.XunitException)
            {
                throw new Xunit.Sdk.XunitException(
                    $"FastWkbWriter output unreadable by '{WkbReaderNames[i]}' on {kind}/{ord}/coords={coords}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(GeometryGenerator.CombinationMatrix), MemberType = typeof(GeometryGenerator))]
    public void NtsWkbWriter_output_readable_by_fast_readers(string kind, Ordinates ord, int coords, int seed)
    {
        var g = GeometryGenerator.Build(kind, ord, coords, seed);
        var ntsLe = NtsWkbW.Write(g, ByteOrder.LittleEndian);
        var ntsBe = NtsWkbW.Write(g, ByteOrder.BigEndian);
        var expected = NtsWkb.Read(ntsLe);

        for (int i = 0; i < WkbReaders.Length; i++)
        {
            try
            {
                CoordinateAsserts.AssertCoordinatesBitEqual(expected, WkbReaders[i].Read(ntsLe));
                CoordinateAsserts.AssertCoordinatesBitEqual(expected, WkbReaders[i].Read(ntsBe));
            }
            catch (Xunit.Sdk.XunitException)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Reader '{WkbReaderNames[i]}' could not read NTS output for {kind}/{ord}/coords={coords}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(GeometryGenerator.CombinationMatrix), MemberType = typeof(GeometryGenerator))]
    public void NtsWktWriter_output_readable_by_fast_readers(string kind, Ordinates ord, int coords, int seed)
    {
        var g = GeometryGenerator.Build(kind, ord, coords, seed);
        var ntsWkt = NtsWktW.Write(g);
        var expected = NtsWkt.Read(ntsWkt);

        for (int i = 0; i < WktReaders.Length; i++)
        {
            try
            {
                CoordinateAsserts.AssertCoordinatesBitEqual(expected, WktReaders[i].Read(ntsWkt), WktUlpTolerance[i]);
            }
            catch (Xunit.Sdk.XunitException)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Reader '{WktReaderNames[i]}' could not read NTS WKT output for {kind}/{ord}/coords={coords}");
            }
        }
    }
}
