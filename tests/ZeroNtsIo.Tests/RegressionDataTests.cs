using ZeroNtsIo.Naive;
using ZeroNtsIo.Reference;
using ZeroNtsIo.Tests.Fixtures;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Xunit;

namespace ZeroNtsIo.Tests;

/// <summary>
/// Regression fixtures sourced from 国土数値情報 (KSJ, 国土交通省, CC BY 4.0).
/// Covers Point / Line / Polygon with multiple real-world datasets each.
/// See bench/Data/README.md for attribution and derivation.
/// </summary>
public class RegressionDataTests
{
    public static IEnumerable<object[]> Polygons() =>
    [
        ["A45_Kagawa_NationalForest.wkb"],
        ["A38_Kagawa_MedicalArea.wkb"],
        ["A03_Kinki_UrbanArea.wkb"],
    ];

    public static IEnumerable<object[]> Lines() =>
    [
        ["N13_Kagawa_Roads.wkb"],
        ["N02_Japan_Railways.wkb"],
    ];

    public static IEnumerable<object[]> Points() =>
    [
        ["P04_Kagawa_Medical.wkb"],
        ["P05_Kagawa_MunicipalHall.wkb"],
        ["P36_Kagawa_ExpresswayBusStops.wkb"],
    ];

    private static readonly (string Name, Func<byte[], Geometry> Read)[] WkbReaders =
    [
        ("Nts",   new NtsWkbReader(Samples.Services).Read),
        ("Naive", new NaiveWkbReader(Samples.Services).Read),
        ("V1",    new Stages.ZWkbReaderV1(Samples.Services).Read),
        ("Z",     new ZWkbReader(Samples.Services).Read),
    ];

    private static readonly (string Name, Func<Geometry, ByteOrder, byte[]> Write)[] WkbWriters =
    [
        ("Nts",   new NtsWkbWriter().Write),
        ("Naive", new NaiveWkbWriter().Write),
        ("Z",     new ZWkbWriter().Write),
    ];

    private static readonly (string Name, Func<Geometry, string> Write)[] WktWriters =
    [
        ("Nts",   new NtsWktWriter().Write),
        ("Naive", new NaiveWktWriter().Write),
        ("Z",     new ZWktWriter().Write),
    ];

    private static readonly (string Name, Func<string, Geometry> Read, long Ulp)[] WktReaders =
    [
        ("Nts",   new NtsWktReader(Samples.Services).Read, 0),
        ("Naive", new NaiveWktReader(Samples.Services).Read, 0),
        ("V1",    new Stages.ZWktReaderV1(Samples.Services).Read, 0),
        ("V2",    new Stages.ZWktReaderV2(Samples.Services).Read, 1),
        ("V3",    new Stages.ZWktReaderV3(Samples.Services).Read, 1),
        ("Z",     new ZWktReader(Samples.Services).Read, 1),
    ];

    [Theory]
    [MemberData(nameof(Polygons))]
    [MemberData(nameof(Lines))]
    [MemberData(nameof(Points))]
    public void All_wkb_readers_match_nts_bit_exact(string fixture)
    {
        var leBytes = RealDataLoader.ReadWkb(fixture);
        var oracle = new NtsWkbReader(Samples.Services).Read(leBytes);
        var beBytes = new NtsWkbWriter().Write(oracle, ByteOrder.BigEndian);

        foreach (var (name, read) in WkbReaders)
        {
            Geometry le, be;
            try { le = read(leBytes); be = read(beBytes); }
            catch (Exception ex)
            {
                throw new Xunit.Sdk.XunitException($"{name} threw on {fixture}: {ex.Message}");
            }
            CoordinateAsserts.AssertCoordinatesBitEqual(oracle, le);
            CoordinateAsserts.AssertCoordinatesBitEqual(oracle, be);
        }
    }

    [Theory]
    [MemberData(nameof(Polygons))]
    [MemberData(nameof(Lines))]
    [MemberData(nameof(Points))]
    public void All_wkb_writers_roundtrip_via_nts_reader(string fixture)
    {
        var leBytes = RealDataLoader.ReadWkb(fixture);
        var oracle = new NtsWkbReader(Samples.Services).Read(leBytes);
        var ntsReader = new NtsWkbReader(Samples.Services);

        foreach (var (wname, write) in WkbWriters)
        foreach (var order in new[] { ByteOrder.LittleEndian, ByteOrder.BigEndian })
        {
            byte[] bytes;
            try { bytes = write(oracle, order); }
            catch (Exception ex)
            {
                throw new Xunit.Sdk.XunitException($"{wname} WKB writer threw on {fixture}/{order}: {ex.Message}");
            }
            var roundtrip = ntsReader.Read(bytes);
            CoordinateAsserts.AssertCoordinatesBitEqual(oracle, roundtrip);
        }
    }

    [Theory]
    [MemberData(nameof(Polygons))]
    [MemberData(nameof(Lines))]
    [MemberData(nameof(Points))]
    public void All_wkt_writers_roundtrip_through_each_reader(string fixture)
    {
        var leBytes = RealDataLoader.ReadWkb(fixture);
        var oracle = new NtsWkbReader(Samples.Services).Read(leBytes);

        foreach (var (wname, write) in WktWriters)
        {
            string text;
            try { text = write(oracle); }
            catch (Exception ex)
            {
                throw new Xunit.Sdk.XunitException($"{wname} WKT writer threw on {fixture}: {ex.Message}");
            }
            foreach (var (rname, read, ulp) in WktReaders)
            {
                Geometry parsed;
                try { parsed = read(text); }
                catch (Exception ex)
                {
                    throw new Xunit.Sdk.XunitException($"{wname}->{rname} WKT interop broken on {fixture}: {ex.Message}");
                }
                CoordinateAsserts.AssertCoordinatesBitEqual(oracle, parsed, ulp);
            }
        }
    }
}
