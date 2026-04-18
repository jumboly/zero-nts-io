using ZeroWkX.Reference;
using NetTopologySuite.IO.ZeroWkX;
using ZeroWkX.Tests.Fixtures;
using Xunit;

namespace ZeroWkX.Tests;

public class EdgeCaseTests
{
    private readonly NtsWktReader _nts = new(Samples.Services);
    private readonly ZWktReader _fast = new(Samples.Services);
    private readonly ZWkbReader _fastWkb = new(Samples.Services);
    private readonly NtsWkbWriter _ntsW = new();

    [Theory]
    [InlineData("point(1 2)")]
    [InlineData("POINT  (  1   2  )")]
    [InlineData("point\t(1\n2)")]
    [InlineData("PoInT (1.0 2.0)")]
    public void Whitespace_and_case_tolerance(string wkt)
    {
        CoordinateAsserts.AssertCoordinatesBitEqual(_nts.Read(wkt), _fast.Read(wkt), ulpTolerance: 1);
    }

    [Fact]
    public void Nested_GeometryCollection()
    {
        const string wkt = "GEOMETRYCOLLECTION (POINT (1 2), GEOMETRYCOLLECTION (LINESTRING (0 0, 1 1), POINT (5 5)))";
        CoordinateAsserts.AssertCoordinatesBitEqual(_nts.Read(wkt), _fast.Read(wkt), ulpTolerance: 1);
    }

    [Fact]
    public void NegativeZero_and_NaN_M_preserved()
    {
        // Why: -0.0 and +0.0 compare equal under `==` but differ in bit pattern; NaN is common in M.
        const string wkt = "LINESTRING M (-0.0 0 NaN, 1 2 3)";
        var g = _fast.Read(wkt);
        var flat = CoordinateAsserts.Flatten(g);
        Assert.Equal(unchecked((long)0x8000000000000000), BitConverter.DoubleToInt64Bits(flat[0]));
        Assert.True(double.IsNaN(flat[2]));
    }

    [Fact]
    public void PointEmpty_wkb_roundtrips_as_nan()
    {
        var g = _nts.Read("POINT EMPTY");
        var wkb = _ntsW.Write(g);
        var round = _fastWkb.Read(wkb);
        Assert.True(round.IsEmpty);
        Assert.Equal("Point", round.GeometryType);
    }

    [Fact]
    public void EWKB_srid_flag_is_rejected()
    {
        // Minimal EWKB header: byte-order=1, type=1 | 0x20000000 (SRID flag set).
        byte[] ewkb = [0x01, 0x01, 0x00, 0x00, 0x20, 0xE6, 0x10, 0x00, 0x00, /* srid */
                      0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,             /* x */
                      0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];            /* y */
        Assert.Throws<FormatException>(() => _fastWkb.Read(ewkb));
    }

    [Theory]
    [InlineData("POINT EMPTY")]
    [InlineData("LINESTRING EMPTY")]
    [InlineData("POLYGON EMPTY")]
    [InlineData("MULTIPOINT EMPTY")]
    [InlineData("MULTIPOLYGON EMPTY")]
    [InlineData("GEOMETRYCOLLECTION EMPTY")]
    public void Empty_variants(string wkt)
    {
        var actual = _fast.Read(wkt);
        Assert.True(actual.IsEmpty);
    }
}
