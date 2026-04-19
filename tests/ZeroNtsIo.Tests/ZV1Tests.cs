using ZeroNtsIo.Stages;
using NetTopologySuite.IO;
using ZeroNtsIo.Reference;
using ZeroNtsIo.Tests.Fixtures;
using Xunit;

namespace ZeroNtsIo.Tests;

public class ZV1Tests
{
    private readonly NtsWktReader _ntsWkt = new(Samples.Services);
    private readonly NtsWkbReader _ntsWkb = new(Samples.Services);
    private readonly ZWktReaderV1 _v1Wkt = new(Samples.Services);
    private readonly ZWkbReaderV1 _v1Wkb = new(Samples.Services);
    private readonly NtsWkbWriter _ntsWkbWriter = new();

    [Theory]
    [MemberData(nameof(Samples.WktAll2D), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZ), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktM), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZM), MemberType = typeof(Samples))]
    public void V1_wkt_matches_nts(string wkt)
    {
        var expected = _ntsWkt.Read(wkt);
        var actual = _v1Wkt.Read(wkt);
        CoordinateAsserts.AssertCoordinatesBitEqual(expected, actual);
    }

    [Theory]
    [MemberData(nameof(Samples.WktAll2D), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZ), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktM), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZM), MemberType = typeof(Samples))]
    public void V1_wkb_matches_nts(string wkt)
    {
        var g = _ntsWkt.Read(wkt);
        var wkbLe = _ntsWkbWriter.Write(g, ByteOrder.LittleEndian);
        var wkbBe = _ntsWkbWriter.Write(g, ByteOrder.BigEndian);

        CoordinateAsserts.AssertCoordinatesBitEqual(g, _v1Wkb.Read(wkbLe));
        CoordinateAsserts.AssertCoordinatesBitEqual(g, _v1Wkb.Read(wkbBe));
    }
}
