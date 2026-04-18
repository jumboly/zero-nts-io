using FastNtsWk.Abstractions;
using FastNtsWk.Fast;
using FastNtsWk.Naive;
using FastNtsWk.Tests.Fixtures;
using Xunit;

namespace FastNtsWk.Tests;

/// <summary>
/// Malformed input must cause an exception (never silently return a corrupted geometry).
/// All custom readers are expected to throw for the same inputs; the exact exception type may
/// differ (FormatException, ArgumentOutOfRangeException, IndexOutOfRangeException, etc.).
/// </summary>
public class MalformedInputTests
{
    private static IEnumerable<IWktReader> WktReaders()
    {
        yield return new NaiveWktReader(Samples.Services);
        yield return new FastWktReaderV1(Samples.Services);
        yield return new FastWktReaderV2(Samples.Services);
        yield return new FastWktReaderV3(Samples.Services);
        yield return new FastWktReaderV4(Samples.Services);
    }

    private static IEnumerable<IWkbReader> WkbReaders()
    {
        yield return new NaiveWkbReader(Samples.Services);
        yield return new FastWkbReaderV1(Samples.Services);
        yield return new FastWkbReaderV4(Samples.Services);
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
        foreach (var reader in WktReaders())
        {
            Assert.ThrowsAny<Exception>(() => reader.Read(wkt));
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
        foreach (var reader in WkbReaders())
        {
            Assert.ThrowsAny<Exception>(() => reader.Read(wkb));
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
        Assert.Throws<FormatException>(() => new FastWkbReaderV1(Samples.Services).Read(ewkb));
        Assert.Throws<FormatException>(() => new FastWkbReaderV4(Samples.Services).Read(ewkb));
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
        foreach (var reader in WkbReaders())
            Assert.ThrowsAny<Exception>(() => reader.Read(wkb));
    }

    [Fact]
    public void Trailing_garbage_after_wkt_is_rejected()
    {
        foreach (var reader in WktReaders())
        {
            Assert.ThrowsAny<Exception>(() => reader.Read("POINT (1 2) extra"));
        }
    }
}
