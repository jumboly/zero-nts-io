using ZeroWkX.Naive;
using ZeroWkX.Reference;
using ZeroWkX.Tests.Fixtures;
using NetTopologySuite.IO;
using Xunit;

namespace ZeroWkX.Tests;

public class NaiveWkbTests
{
    private readonly NtsWktReader _ntsWkt = new(Samples.Services);
    private readonly NtsWkbReader _ntsWkb = new(Samples.Services);
    private readonly NaiveWkbReader _naiveWkb = new(Samples.Services);
    private readonly NtsWkbWriter _ntsWkbWriter = new();
    private readonly NaiveWkbWriter _naiveWkbWriter = new();

    [Theory]
    [MemberData(nameof(Samples.WktAll2D), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZ), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktM), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZM), MemberType = typeof(Samples))]
    public void NtsWritten_Wkb_reads_back_with_naive(string wkt)
    {
        var g = _ntsWkt.Read(wkt);
        var wkbLe = _ntsWkbWriter.Write(g, ByteOrder.LittleEndian);
        var wkbBe = _ntsWkbWriter.Write(g, ByteOrder.BigEndian);

        var roundLe = _naiveWkb.Read(wkbLe);
        var roundBe = _naiveWkb.Read(wkbBe);

        CoordinateAsserts.AssertCoordinatesBitEqual(g, roundLe);
        CoordinateAsserts.AssertCoordinatesBitEqual(g, roundBe);
    }

    [Theory]
    [MemberData(nameof(Samples.WktAll2D), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZ), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktM), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZM), MemberType = typeof(Samples))]
    public void NaiveWritten_Wkb_reads_back_with_nts(string wkt)
    {
        var g = _ntsWkt.Read(wkt);
        var wkbLe = _naiveWkbWriter.Write(g, ByteOrder.LittleEndian);
        var wkbBe = _naiveWkbWriter.Write(g, ByteOrder.BigEndian);

        var roundLe = _ntsWkb.Read(wkbLe);
        var roundBe = _ntsWkb.Read(wkbBe);

        CoordinateAsserts.AssertCoordinatesBitEqual(g, roundLe);
        CoordinateAsserts.AssertCoordinatesBitEqual(g, roundBe);
    }
}
