using FastNtsWk.Abstractions;
using FastNtsWk.Fast;
using FastNtsWk.Naive;
using FastNtsWk.Reference;
using FastNtsWk.Tests.Fixtures;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Xunit;

namespace FastNtsWk.Tests;

/// <summary>
/// Exercises numeric edge cases (NaN, ±∞, subnormals, extreme magnitudes, -0.0, integer vs
/// fractional forms, scientific notation) against the NTS oracle for every reader.
/// </summary>
public class NumericEdgeTests
{
    private static readonly IWktReader NtsWkt = new NtsWktReader(Samples.Services);
    private static readonly IWkbReader NtsWkb = new NtsWkbReader(Samples.Services);
    private static readonly IWkbWriter NtsWkbW = new NtsWkbWriter();

    private static readonly (string Name, IWktReader R, long Ulp)[] WktReaders =
    [
        ("Naive", new NaiveWktReader(Samples.Services), 0),
        ("V1", new FastWktReaderV1(Samples.Services), 0),
        ("V2", new FastWktReaderV2(Samples.Services), 1),
        ("V3", new FastWktReaderV3(Samples.Services), 1),
        ("V4", new FastWktReaderV4(Samples.Services), 1),
    ];

    public static IEnumerable<object[]> ExtremeDoubleWkts()
    {
        // Why: picks one representative WKT per tricky double value — zero variants, extreme
        // magnitudes, subnormals, specials, explicit positive signs, and integer/scientific forms.
        yield return new object[] { "POINT (0 0)" };
        yield return new object[] { "POINT (-0 -0)" };
        yield return new object[] { "POINT (1 2)" };
        yield return new object[] { "POINT (+1 +2)" };
        yield return new object[] { "POINT (1.0 2.0)" };
        yield return new object[] { "POINT (1e10 2e-10)" };
        yield return new object[] { "POINT (1.5E+3 -2.5E-3)" };
        yield return new object[] { "POINT (1.7976931348623157E+308 -1.7976931348623157E+308)" }; // near double max
        yield return new object[] { "POINT (5E-324 -5E-324)" }; // smallest subnormal
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
        foreach (var (name, reader, ulp) in WktReaders)
        {
            try { CoordinateAsserts.AssertCoordinatesBitEqual(expected, reader.Read(wkt), ulp); }
            catch (Xunit.Sdk.XunitException) { throw new Xunit.Sdk.XunitException($"{name} diverged on: {wkt}"); }
        }
    }

    [Fact]
    public void NegativeZero_bit_pattern_is_preserved_through_wkb()
    {
        // Why: -0.0 and +0.0 compare equal under ==; only the bit pattern differentiates them.
        // Round-tripping via WKB must preserve the sign bit for data correctness.
        const long negZeroBits = unchecked((long)0x8000000000000000);
        var expected = BitConverter.Int64BitsToDouble(negZeroBits);

        var g = NtsWkt.Read("POINT (-0.0 -0.0)");
        var wkb = NtsWkbW.Write(g);

        foreach (var reader in new IWkbReader[]
        {
            new FastWkbReaderV1(Samples.Services),
            new FastWkbReaderV4(Samples.Services),
            new NaiveWkbReader(Samples.Services),
        })
        {
            var p = (Point)reader.Read(wkb);
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
            var wkb = new FastWkbWriter().Write(p, order);
            var readers = new IWkbReader[]
            {
                NtsWkb,
                new NaiveWkbReader(Samples.Services),
                new FastWkbReaderV1(Samples.Services),
                new FastWkbReaderV4(Samples.Services),
            };
            foreach (var r in readers)
            {
                var back = (Point)r.Read(wkb);
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
        foreach (var (name, reader, ulp) in WktReaders)
        {
            try { CoordinateAsserts.AssertCoordinatesBitEqual(expected, reader.Read(wkt), ulp); }
            catch (Xunit.Sdk.XunitException) { throw new Xunit.Sdk.XunitException($"{name} diverged on: {wkt}"); }
        }
    }
}
