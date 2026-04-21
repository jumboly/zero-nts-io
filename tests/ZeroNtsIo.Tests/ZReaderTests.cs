using ZeroNtsIo.Reference;
using NetTopologySuite.IO;
using ZeroNtsIo.Tests.Fixtures;
using Xunit;

namespace ZeroNtsIo.Tests;

/// <summary>
/// 公開版 <see cref="ZWktReader"/> / <see cref="ZWkbReader"/> API の smoke テスト。
/// 広範なカバレッジは PropertyBasedTests と WriterInteropTests に置いている。
/// </summary>
public class ZReaderTests
{
    private readonly NtsWktReader _ntsWkt = new(Samples.Services);
    private readonly ZWktReader _fastWkt = new(Samples.Services);
    private readonly ZWkbReader _fastWkb = new(Samples.Services);
    private readonly NtsWkbWriter _ntsWkbWriter = new();

    [Theory]
    [MemberData(nameof(Samples.WktAll2D), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZ), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktM), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZM), MemberType = typeof(Samples))]
    public void Fast_wkt_matches_nts_within_1_ulp(string wkt)
    {
        var expected = _ntsWkt.Read(wkt);
        var actual = _fastWkt.Read(wkt);
        CoordinateAsserts.AssertCoordinatesBitEqual(expected, actual, ulpTolerance: 1);
    }

    [Theory]
    [MemberData(nameof(Samples.WktAll2D), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZ), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktM), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZM), MemberType = typeof(Samples))]
    public void Fast_wkb_matches_nts(string wkt)
    {
        var g = _ntsWkt.Read(wkt);
        var wkbLe = _ntsWkbWriter.Write(g, ByteOrder.LittleEndian);
        var wkbBe = _ntsWkbWriter.Write(g, ByteOrder.BigEndian);

        CoordinateAsserts.AssertCoordinatesBitEqual(g, _fastWkb.Read(wkbLe));
        CoordinateAsserts.AssertCoordinatesBitEqual(g, _fastWkb.Read(wkbBe));
    }
}
