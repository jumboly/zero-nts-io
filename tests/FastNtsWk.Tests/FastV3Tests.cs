using FastNtsWk.Abstractions;
using FastNtsWk.Fast;
using FastNtsWk.Reference;
using FastNtsWk.Tests.Fixtures;
using Xunit;

namespace FastNtsWk.Tests;

public class FastV3Tests
{
    private readonly IWktReader _nts = new NtsWktReader(Samples.Services);
    private readonly IWktReader _v3 = new FastWktReaderV3(Samples.Services);

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
