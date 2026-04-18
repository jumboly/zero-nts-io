using FastNtsWk.Abstractions;
using FastNtsWk.Fast;
using FastNtsWk.Reference;
using FastNtsWk.Tests.Fixtures;
using NetTopologySuite.IO;
using Xunit;

namespace FastNtsWk.Tests;

public class FastWriterTests
{
    private readonly IWktReader _ntsWkt = new NtsWktReader(Samples.Services);
    private readonly IWkbReader _ntsWkb = new NtsWkbReader(Samples.Services);
    private readonly IWktWriter _fastWktW = new FastWktWriter();
    private readonly IWkbWriter _fastWkbW = new FastWkbWriter();

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
