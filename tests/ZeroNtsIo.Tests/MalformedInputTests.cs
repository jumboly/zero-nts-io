using ZeroNtsIo.Stages;
using NetTopologySuite.IO;
using ZeroNtsIo.Naive;
using ZeroNtsIo.Tests.Fixtures;
using NetTopologySuite.Geometries;
using Xunit;

namespace ZeroNtsIo.Tests;

/// <summary>
/// 不正な入力は必ず例外を発生させること（黙って壊れたジオメトリを返してはならない）。
/// 全カスタム Reader が同じ入力で例外を投げることを要求する。例外型は実装により異なってよい
/// （FormatException / ArgumentOutOfRangeException / IndexOutOfRangeException など）。
/// </summary>
public class MalformedInputTests
{
    private static IEnumerable<Func<string, Geometry>> WktReaders()
    {
        yield return new NaiveWktReader(Samples.Services).Read;
        yield return new ZWktReaderV1(Samples.Services).Read;
        yield return new ZWktReaderV2(Samples.Services).Read;
        yield return new ZWktReaderV3(Samples.Services).Read;
        yield return new ZWktReader(Samples.Services).Read;
    }

    private static IEnumerable<Func<byte[], Geometry>> WkbReaders()
    {
        yield return new NaiveWkbReader(Samples.Services).Read;
        yield return new ZWkbReaderV1(Samples.Services).Read;
        yield return new ZWkbReader(Samples.Services).Read;
    }

    [Theory]
    [InlineData("POINT")]                          // 本体無し
    [InlineData("POINT (")]                        // 閉じ括弧無し
    [InlineData("POINT 1 2)")]                     // 開き括弧無し
    [InlineData("POINT (1)")]                      // ordinate が 1 つしかない
    [InlineData("POINT (1 2")]                     // 閉じ括弧無し
    [InlineData("NOTAGEOM (1 2)")]                 // 未知のキーワード
    [InlineData("POINT (foo bar)")]                // 非数値
    [InlineData("POLYGON ((0 0, 1 0, 1 1, 0 0)")]  // 外周の閉じ括弧無し
    [InlineData("")]                               // 空入力
    [InlineData("   ")]                            // 空白のみ
    public void Malformed_wkt_throws(string wkt)
    {
        foreach (var read in WktReaders())
        {
            Assert.ThrowsAny<Exception>(() => read(wkt));
        }
    }

    [Theory]
    [InlineData(new byte[] { })]                                            // 空
    [InlineData(new byte[] { 1 })]                                          // byte order のみ
    [InlineData(new byte[] { 2, 1, 0, 0, 0 })]                              // 不正な byte order バイト
    [InlineData(new byte[] { 1, 99, 0, 0, 0 })]                             // 未知のジオメトリ型
    [InlineData(new byte[] { 1, 1, 0, 0, 0, 0, 0, 0 })]                     // Point の座標 16 バイトが途中で切れている
    public void Malformed_wkb_throws(byte[] wkb)
    {
        foreach (var read in WkbReaders())
        {
            Assert.ThrowsAny<Exception>(() => read(wkb));
        }
    }

    [Fact]
    public void Ewkb_srid_flag_is_rejected_by_strict_ogc_readers()
    {
        // Why: Naive / stage V1 Reader は教材用のベースラインで、厳密 OGC を維持する。
        // 公開版 ZWkbReader は EWKB を受理する（正常系のカバレッジは EwkbTests に置いている）。
        byte[] ewkb =
        [
            0x01,                                           // LE
            0x01, 0x00, 0x00, 0x20,                         // type 1 | 0x20000000（SRID フラグ）
            0xE6, 0x10, 0x00, 0x00,                         // SRID 4326
            0, 0, 0, 0, 0, 0, 0, 0,                         // X
            0, 0, 0, 0, 0, 0, 0, 0,                         // Y
        ];
        Assert.Throws<FormatException>(() => new ZWkbReaderV1(Samples.Services).Read(ewkb));
        Assert.Throws<FormatException>(() => new NaiveWkbReader(Samples.Services).Read(ewkb));
    }

    [Fact]
    public void Wkb_with_negative_count_is_rejected()
    {
        // Why: uint32 0xFFFFFFFF（int にすると -1）は検証しないと巨大な Coordinate[] を確保してしまう。
        // 早い段階で検出して失敗させる。
        byte[] wkb =
        [
            0x01,                                           // LE
            0x02, 0x00, 0x00, 0x00,                         // LineString 2D
            0xFF, 0xFF, 0xFF, 0xFF,                         // count = uint.MaxValue
        ];
        foreach (var read in WkbReaders())
            Assert.ThrowsAny<Exception>(() => read(wkb));
    }

    [Fact]
    public void Trailing_garbage_after_wkt_is_rejected()
    {
        foreach (var read in WktReaders())
        {
            Assert.ThrowsAny<Exception>(() => read("POINT (1 2) extra"));
        }
    }
}
