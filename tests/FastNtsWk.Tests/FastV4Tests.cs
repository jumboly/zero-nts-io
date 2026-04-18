using FastNtsWk.Abstractions;
using FastNtsWk.Fast;
using FastNtsWk.Reference;
using FastNtsWk.Tests.Fixtures;
using NetTopologySuite.IO;
using Xunit;

namespace FastNtsWk.Tests;

public class FastV4Tests
{
    private readonly IWktReader _ntsWkt = new NtsWktReader(Samples.Services);
    private readonly IWkbReader _ntsWkb = new NtsWkbReader(Samples.Services);
    private readonly IWktReader _v4Wkt = new FastWktReaderV4(Samples.Services);
    private readonly IWkbReader _v4Wkb = new FastWkbReaderV4(Samples.Services);
    private readonly IWkbWriter _ntsWkbWriter = new NtsWkbWriter();

    [Theory]
    [MemberData(nameof(Samples.WktAll2D), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZ), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktM), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZM), MemberType = typeof(Samples))]
    public void V4_wkt_matches_nts_within_1_ulp(string wkt)
    {
        var expected = _ntsWkt.Read(wkt);
        var actual = _v4Wkt.Read(wkt);
        CoordinateAsserts.AssertCoordinatesBitEqual(expected, actual, ulpTolerance: 1);
    }

    [Theory]
    [MemberData(nameof(Samples.WktAll2D), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZ), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktM), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZM), MemberType = typeof(Samples))]
    public void V4_wkb_matches_nts(string wkt)
    {
        var g = _ntsWkt.Read(wkt);
        var wkbLe = _ntsWkbWriter.Write(g, ByteOrder.LittleEndian);
        var wkbBe = _ntsWkbWriter.Write(g, ByteOrder.BigEndian);

        CoordinateAsserts.AssertCoordinatesBitEqual(g, _v4Wkb.Read(wkbLe));
        CoordinateAsserts.AssertCoordinatesBitEqual(g, _v4Wkb.Read(wkbBe));
    }
}
