using FastNtsWk.Abstractions;
using FastNtsWk.Fast;
using FastNtsWk.Reference;
using FastNtsWk.Tests.Fixtures;
using Xunit;

namespace FastNtsWk.Tests;

public class FastV2Tests
{
    private readonly IWktReader _nts = new NtsWktReader(Samples.Services);
    private readonly IWktReader _v2 = new FastWktReaderV2(Samples.Services);

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
