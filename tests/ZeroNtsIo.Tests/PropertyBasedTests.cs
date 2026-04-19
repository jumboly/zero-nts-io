using ZeroNtsIo.Stages;
using NetTopologySuite.IO;
using ZeroNtsIo.Naive;
using ZeroNtsIo.Reference;
using ZeroNtsIo.Tests.Fixtures;
using NetTopologySuite.Geometries;
using Xunit;

namespace ZeroNtsIo.Tests;

/// <summary>
/// Reads generated geometries through every implementation and asserts bit-for-bit coordinate
/// equality against the NTS oracle. Covers every geometry type × every dimension × three sizes.
/// </summary>
public class PropertyBasedTests
{
    private static readonly NtsWktWriter NtsWktW = new();
    private static readonly NtsWkbWriter NtsWkbW = new();

    private static readonly NtsWktReader NtsWkt = new(Samples.Services);
    private static readonly NtsWkbReader NtsWkb = new(Samples.Services);

    // Why: the custom double parser (FastDoubleParser) can round differently from BCL by up to
    // 1 ULP; V1 and Naive use BCL directly and are 0 ULP. WKB has no text parsing so 0 ULP everywhere.
    private static readonly (string Name, Func<string, Geometry> Read, long Ulp)[] WktReaders =
    [
        ("Naive", new NaiveWktReader(Samples.Services).Read, 0),
        ("V1",    new ZWktReaderV1(Samples.Services).Read, 0),
        ("V2",    new ZWktReaderV2(Samples.Services).Read, 1),
        ("V3",    new ZWktReaderV3(Samples.Services).Read, 1),
        ("Fast",  new ZWktReader(Samples.Services).Read,   1),
    ];

    private static readonly (string Name, Func<byte[], Geometry> Read)[] WkbReaders =
    [
        ("Naive", new NaiveWkbReader(Samples.Services).Read),
        ("V1",    new ZWkbReaderV1(Samples.Services).Read),
        ("Fast",  new ZWkbReader(Samples.Services).Read),
    ];

    [Theory]
    [MemberData(nameof(GeometryGenerator.CombinationMatrix), MemberType = typeof(GeometryGenerator))]
    public void Wkt_read_matches_nts_for_every_impl(string kind, Ordinates ord, int coords, int seed)
    {
        var g = GeometryGenerator.Build(kind, ord, coords, seed);
        var wkt = NtsWktW.Write(g);
        var expected = NtsWkt.Read(wkt);

        foreach (var (name, read, ulp) in WktReaders)
        {
            var actual = read(wkt);
            try
            {
                CoordinateAsserts.AssertCoordinatesBitEqual(expected, actual, ulp);
            }
            catch (Xunit.Sdk.XunitException)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Reader '{name}' diverged on {kind}/{ord}/coords={coords}");
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

        foreach (var (name, read) in WkbReaders)
        {
            try
            {
                CoordinateAsserts.AssertCoordinatesBitEqual(expected, read(wkbLe));
                CoordinateAsserts.AssertCoordinatesBitEqual(expected, read(wkbBe));
            }
            catch (Xunit.Sdk.XunitException)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Reader '{name}' diverged on {kind}/{ord}/coords={coords}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(GeometryGenerator.CombinationMatrix), MemberType = typeof(GeometryGenerator))]
    public void ZWktWriter_output_roundtrips_through_all_readers(string kind, Ordinates ord, int coords, int seed)
    {
        var g = GeometryGenerator.Build(kind, ord, coords, seed);
        var fastWkt = new ZWktWriter().Write(g);

        foreach (var (name, read, ulp) in WktReaders)
        {
            var parsed = read(fastWkt);
            try
            {
                CoordinateAsserts.AssertCoordinatesBitEqual(g, parsed, ulp);
            }
            catch (Xunit.Sdk.XunitException)
            {
                throw new Xunit.Sdk.XunitException(
                    $"ZWktWriter output unreadable by '{name}' on {kind}/{ord}/coords={coords}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(GeometryGenerator.CombinationMatrix), MemberType = typeof(GeometryGenerator))]
    public void ZWkbWriter_output_roundtrips_through_all_readers(string kind, Ordinates ord, int coords, int seed)
    {
        var g = GeometryGenerator.Build(kind, ord, coords, seed);
        var fastLe = new ZWkbWriter().Write(g, ByteOrder.LittleEndian);
        var fastBe = new ZWkbWriter().Write(g, ByteOrder.BigEndian);

        foreach (var (name, read) in WkbReaders)
        {
            try
            {
                CoordinateAsserts.AssertCoordinatesBitEqual(g, read(fastLe));
                CoordinateAsserts.AssertCoordinatesBitEqual(g, read(fastBe));
            }
            catch (Xunit.Sdk.XunitException)
            {
                throw new Xunit.Sdk.XunitException(
                    $"ZWkbWriter output unreadable by '{name}' on {kind}/{ord}/coords={coords}");
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

        foreach (var (name, read) in WkbReaders)
        {
            try
            {
                CoordinateAsserts.AssertCoordinatesBitEqual(expected, read(ntsLe));
                CoordinateAsserts.AssertCoordinatesBitEqual(expected, read(ntsBe));
            }
            catch (Xunit.Sdk.XunitException)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Reader '{name}' could not read NTS output for {kind}/{ord}/coords={coords}");
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

        foreach (var (name, read, ulp) in WktReaders)
        {
            try
            {
                CoordinateAsserts.AssertCoordinatesBitEqual(expected, read(ntsWkt), ulp);
            }
            catch (Xunit.Sdk.XunitException)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Reader '{name}' could not read NTS WKT output for {kind}/{ord}/coords={coords}");
            }
        }
    }
}
