using ZeroNtsIo.Reference;
using NetTopologySuite.IO;
using ZeroNtsIo.Tests.Fixtures;
using Xunit;

namespace ZeroNtsIo.Tests;

public class ZWriterTests
{
    private readonly NtsWktReader _ntsWkt = new(Samples.Services);
    private readonly NtsWkbReader _ntsWkb = new(Samples.Services);
    private readonly ZWktWriter _fastWktW = new();
    private readonly ZWkbWriter _fastWkbW = new();

    [Theory]
    [MemberData(nameof(Samples.WktAll2D), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZ), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktM), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZM), MemberType = typeof(Samples))]
    public void FastWkt_roundtrips_through_nts(string wkt)
    {
        var g = _ntsWkt.Read(wkt);
        var fastWkt = _fastWktW.Write(g);
        var parsedBack = _ntsWkt.Read(fastWkt);
        CoordinateAsserts.AssertCoordinatesBitEqual(g, parsedBack);
    }

    [Theory]
    [MemberData(nameof(Samples.WktAll2D), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZ), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktM), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZM), MemberType = typeof(Samples))]
    public void FastWkb_roundtrips_through_nts_le(string wkt)
    {
        var g = _ntsWkt.Read(wkt);
        var wkb = _fastWkbW.Write(g, ByteOrder.LittleEndian);
        var parsedBack = _ntsWkb.Read(wkb);
        CoordinateAsserts.AssertCoordinatesBitEqual(g, parsedBack);
    }

    [Theory]
    [MemberData(nameof(Samples.WktAll2D), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZ), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktM), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZM), MemberType = typeof(Samples))]
    public void FastWkb_roundtrips_through_nts_be(string wkt)
    {
        var g = _ntsWkt.Read(wkt);
        var wkb = _fastWkbW.Write(g, ByteOrder.BigEndian);
        var parsedBack = _ntsWkb.Read(wkb);
        CoordinateAsserts.AssertCoordinatesBitEqual(g, parsedBack);
    }
}
