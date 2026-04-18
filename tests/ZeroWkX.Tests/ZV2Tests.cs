using ZeroWkX.Stages;
using NetTopologySuite.IO.ZeroWkX;
using ZeroWkX.Reference;
using ZeroWkX.Tests.Fixtures;
using Xunit;

namespace ZeroWkX.Tests;

public class ZV2Tests
{
    private readonly NtsWktReader _nts = new(Samples.Services);
    private readonly ZWktReaderV2 _v2 = new(Samples.Services);

    [Theory]
    [MemberData(nameof(Samples.WktAll2D), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZ), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktM), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZM), MemberType = typeof(Samples))]
    public void V2_wkt_matches_nts_within_1_ulp(string wkt)
    {
        var expected = _nts.Read(wkt);
        var actual = _v2.Read(wkt);
        // Why: the custom double parser routes simple decimals through mantissa*pow10[exp];
        // for values that fit the fast path this is exact, but rounding can differ by 1 ULP
        // versus the BCL parser at the edges.
        CoordinateAsserts.AssertCoordinatesBitEqual(expected, actual, ulpTolerance: 1);
    }
}
