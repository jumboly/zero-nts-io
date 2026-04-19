using ZeroNtsIo.Stages;
using NetTopologySuite.IO;
using ZeroNtsIo.Reference;
using ZeroNtsIo.Tests.Fixtures;
using Xunit;

namespace ZeroNtsIo.Tests;

public class ZV3Tests
{
    private readonly NtsWktReader _nts = new(Samples.Services);
    private readonly ZWktReaderV3 _v3 = new(Samples.Services);

    [Theory]
    [MemberData(nameof(Samples.WktAll2D), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZ), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktM), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZM), MemberType = typeof(Samples))]
    public void V3_wkt_matches_nts_within_1_ulp(string wkt)
    {
        var expected = _nts.Read(wkt);
        var actual = _v3.Read(wkt);
        CoordinateAsserts.AssertCoordinatesBitEqual(expected, actual, ulpTolerance: 1);
    }
}
