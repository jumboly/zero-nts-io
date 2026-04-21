using ZeroNtsIo.Reference;
using NetTopologySuite.IO;
using ZeroNtsIo.Tests.Fixtures;
using Xunit;

namespace ZeroNtsIo.Tests;

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
        // Why: -0.0 と +0.0 は `==` では等しいがビットパターンが異なる。M 座標では NaN もよく使われる。
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
    public void EWKB_srid_flag_is_accepted_and_preserved()
    {
        // Why: EWKB (PostGIS) はタイプコードの高位ビット 0x20000000 に SRID フラグを載せる。
        // ZWkbReader は NTS と同じく、フラグを受理し、返すジオメトリに SRID を反映する。
        byte[] ewkb = [0x01, 0x01, 0x00, 0x00, 0x20, 0xE6, 0x10, 0x00, 0x00, /* SRID 4326 */
                      0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,             /* X */
                      0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];            /* Y */
        var g = _fastWkb.Read(ewkb);
        Assert.Equal("Point", g.GeometryType);
        Assert.Equal(4326, g.SRID);
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
