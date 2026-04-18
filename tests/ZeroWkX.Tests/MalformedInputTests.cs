using ZeroWkX.Stages;
using NetTopologySuite.IO;
using ZeroWkX.Naive;
using ZeroWkX.Tests.Fixtures;
using NetTopologySuite.Geometries;
using Xunit;

namespace ZeroWkX.Tests;

/// <summary>
/// Malformed input must cause an exception (never silently return a corrupted geometry).
/// All custom readers are expected to throw for the same inputs; the exact exception type may
/// differ (FormatException, ArgumentOutOfRangeException, IndexOutOfRangeException, etc.).
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
    [InlineData("POINT")]                          // no body
    [InlineData("POINT (")]                        // unclosed paren
    [InlineData("POINT 1 2)")]                     // missing open paren
    [InlineData("POINT (1)")]                      // only 1 ordinate
    [InlineData("POINT (1 2")]                     // missing close paren
    [InlineData("NOTAGEOM (1 2)")]                 // unknown keyword
    [InlineData("POINT (foo bar)")]                // non-numeric
    [InlineData("POLYGON ((0 0, 1 0, 1 1, 0 0)")]  // unclosed outer
    [InlineData("")]                               // empty input
    [InlineData("   ")]                            // whitespace only
    public void Malformed_wkt_throws(string wkt)
    {
        foreach (var read in WktReaders())
        {
            Assert.ThrowsAny<Exception>(() => read(wkt));
        }
    }

    [Theory]
    [InlineData(new byte[] { })]                                            // empty
    [InlineData(new byte[] { 1 })]                                          // only byte order
    [InlineData(new byte[] { 2, 1, 0, 0, 0 })]                              // invalid byte order byte
    [InlineData(new byte[] { 1, 99, 0, 0, 0 })]                             // unknown geometry type
    [InlineData(new byte[] { 1, 1, 0, 0, 0, 0, 0, 0 })]                     // Point truncated (needs 16 bytes of doubles)
    public void Malformed_wkb_throws(byte[] wkb)
    {
        foreach (var read in WkbReaders())
        {
            Assert.ThrowsAny<Exception>(() => read(wkb));
        }
    }

    [Fact]
    public void Ewkb_srid_flag_is_rejected_by_fast_readers()
    {
        // Why: PostGIS EWKB encodes SRID via the 0x20000000 type-code high bit. We target OGC
        // ISO only; silently ignoring the flag would misread geometries with SRID data.
        byte[] ewkb =
        [
            0x01,                                           // LE
            0x01, 0x00, 0x00, 0x20,                         // type 1 | 0x20000000 (SRID flag)
            0xE6, 0x10, 0x00, 0x00,                         // SRID 4326
            0, 0, 0, 0, 0, 0, 0, 0,                         // X
            0, 0, 0, 0, 0, 0, 0, 0,                         // Y
        ];
        Assert.Throws<FormatException>(() => new ZWkbReaderV1(Samples.Services).Read(ewkb));
        Assert.Throws<FormatException>(() => new ZWkbReader(Samples.Services).Read(ewkb));
        Assert.Throws<FormatException>(() => new NaiveWkbReader(Samples.Services).Read(ewkb));
    }

    [Fact]
    public void Wkb_with_negative_count_is_rejected()
    {
        // Why: uint32 0xFFFFFFFF (treated as int -1) would allocate a huge Coordinate[] if not
        // validated — detect and fail fast.
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
