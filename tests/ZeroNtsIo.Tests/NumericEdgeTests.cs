using ZeroNtsIo.Stages;
using NetTopologySuite.IO;
using ZeroNtsIo.Naive;
using ZeroNtsIo.Reference;
using ZeroNtsIo.Tests.Fixtures;
using NetTopologySuite.Geometries;
using Xunit;

namespace ZeroNtsIo.Tests;

/// <summary>
/// 数値のエッジケース（NaN / ±∞ / subnormal / 極端な大小 / -0.0 / 整数 vs 小数表記 / 科学表記）を
/// 全 Reader について NTS オラクルと比較する。
/// </summary>
public class NumericEdgeTests
{
    private static readonly NtsWktReader NtsWkt = new(Samples.Services);
    private static readonly NtsWkbReader NtsWkb = new(Samples.Services);
    private static readonly NtsWkbWriter NtsWkbW = new();

    // Why: カスタム double パーサ (FastDoubleParser) は高速経路で BCL と最大 1 ULP まで丸めが異なり得る。
    // V1 と Naive は BCL をそのまま使うため 0 ULP。
    private static readonly (string Name, Func<string, Geometry> Read, long Ulp)[] WktReaders =
    [
        ("Naive", new NaiveWktReader(Samples.Services).Read, 0),
        ("V1",    new ZWktReaderV1(Samples.Services).Read, 0),
        ("V2",    new ZWktReaderV2(Samples.Services).Read, 1),
        ("V3",    new ZWktReaderV3(Samples.Services).Read, 1),
        ("Fast",  new ZWktReader(Samples.Services).Read,   1),
    ];

    public static IEnumerable<object[]> ExtremeDoubleWkts()
    {
        // Why: 扱いに注意が必要な double 値（0 の各種形式・極端な大小・subnormal・特殊値・明示的な正符号・
        // 整数/科学表記）それぞれについて代表的な WKT を 1 本ずつ選んでいる。
        yield return new object[] { "POINT (0 0)" };
        yield return new object[] { "POINT (-0 -0)" };
        yield return new object[] { "POINT (1 2)" };
        yield return new object[] { "POINT (+1 +2)" };
        yield return new object[] { "POINT (1.0 2.0)" };
        yield return new object[] { "POINT (1e10 2e-10)" };
        yield return new object[] { "POINT (1.5E+3 -2.5E-3)" };
        yield return new object[] { "POINT (1.7976931348623157E+308 -1.7976931348623157E+308)" }; // double 最大値付近
        yield return new object[] { "POINT (5E-324 -5E-324)" }; // 最小 subnormal
        yield return new object[] { "POINT (0.1 0.2)" };
        yield return new object[] { "POINT (123456789.987654321 987654321.123456789)" };
        yield return new object[] { "LINESTRING M (0 0 NaN, 1 1 NaN)" };
        yield return new object[] { "LINESTRING M (0 0 Infinity, 1 1 -Infinity)" };
        yield return new object[] { "LINESTRING (0 0, 1e-300 1e300)" };
    }

    [Theory]
    [MemberData(nameof(ExtremeDoubleWkts))]
    public void Every_reader_parses_extreme_numbers_like_nts(string wkt)
    {
        var expected = NtsWkt.Read(wkt);
        foreach (var (name, read, ulp) in WktReaders)
        {
            try { CoordinateAsserts.AssertCoordinatesBitEqual(expected, read(wkt), ulp); }
            catch (Xunit.Sdk.XunitException) { throw new Xunit.Sdk.XunitException($"{name} diverged on: {wkt}"); }
        }
    }

    [Fact]
    public void NegativeZero_bit_pattern_is_preserved_through_wkb()
    {
        // Why: -0.0 と +0.0 は == では等しく、区別できるのはビットパターンのみ。
        // データの正しさを保つため、WKB ラウンドトリップで符号ビットは保持されなければならない。
        const long negZeroBits = unchecked((long)0x8000000000000000);

        var g = NtsWkt.Read("POINT (-0.0 -0.0)");
        var wkb = NtsWkbW.Write(g);

        var readers = new Func<byte[], Geometry>[]
        {
            new ZWkbReaderV1(Samples.Services).Read,
            new ZWkbReader(Samples.Services).Read,
            new NaiveWkbReader(Samples.Services).Read,
        };
        foreach (var read in readers)
        {
            var p = (Point)read(wkb);
            Assert.Equal(negZeroBits, BitConverter.DoubleToInt64Bits(p.X));
            Assert.Equal(negZeroBits, BitConverter.DoubleToInt64Bits(p.Y));
        }
    }

    [Theory]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    [InlineData(double.Epsilon)]
    [InlineData(-double.Epsilon)]
    [InlineData(0.1 + 0.2)]
    [InlineData(Math.PI)]
    [InlineData(Math.E)]
    public void Wkb_roundtrip_preserves_exact_double_bits(double value)
    {
        var p = Samples.Factory.CreatePoint(new Coordinate(value, value));
        foreach (var order in new[] { ByteOrder.LittleEndian, ByteOrder.BigEndian })
        {
            var wkb = new ZWkbWriter().Write(p, order);
            var readers = new Func<byte[], Geometry>[]
            {
                NtsWkb.Read,
                new NaiveWkbReader(Samples.Services).Read,
                new ZWkbReaderV1(Samples.Services).Read,
                new ZWkbReader(Samples.Services).Read,
            };
            foreach (var read in readers)
            {
                var back = (Point)read(wkb);
                Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(back.X));
            }
        }
    }

    [Theory]
    [InlineData("POINT(1 2)")]
    [InlineData("POINT ( 1 2 )")]
    [InlineData("POINT\t(1\t2)")]
    [InlineData("POINT\n(1\n2)")]
    [InlineData("POINT\r\n(1\r\n2)")]
    [InlineData("point(1 2)")]
    [InlineData("Point(1 2)")]
    [InlineData("PoInT(1 2)")]
    [InlineData("point Z (1 2 3)")]
    [InlineData("POLYGON((0 0,1 0,1 1,0 0))")]
    [InlineData("POLYGON(\t(0 0, 1 0, 1 1, 0 0)\t)")]
    public void Whitespace_and_case_tolerance_matches_nts(string wkt)
    {
        var expected = NtsWkt.Read(wkt);
        foreach (var (name, read, ulp) in WktReaders)
        {
            try { CoordinateAsserts.AssertCoordinatesBitEqual(expected, read(wkt), ulp); }
            catch (Xunit.Sdk.XunitException) { throw new Xunit.Sdk.XunitException($"{name} diverged on: {wkt}"); }
        }
    }
}
