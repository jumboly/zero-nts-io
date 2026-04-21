using ZeroNtsIo.Stages;
using NetTopologySuite.IO;
using ZeroNtsIo.Reference;
using ZeroNtsIo.Tests.Fixtures;
using Xunit;

namespace ZeroNtsIo.Tests;

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
        // Why: カスタム double パーサは単純な小数を mantissa*pow10[exp] 経路で処理する。
        // 高速経路に収まる値は厳密だが、境界値では BCL パーサとの丸め差が最大 1 ULP 生じうる。
        CoordinateAsserts.AssertCoordinatesBitEqual(expected, actual, ulpTolerance: 1);
    }
}
