using FastNtsWk.Abstractions;
using FastNtsWk.Naive;
using FastNtsWk.Reference;
using FastNtsWk.Tests.Fixtures;
using Xunit;

namespace FastNtsWk.Tests;

public class NaiveWktTests
{
    private readonly IWktReader _nts = new NtsWktReader(Samples.Services);
    private readonly IWktReader _naive = new NaiveWktReader(Samples.Services);

    [Theory]
    [MemberData(nameof(Samples.WktAll2D), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZ), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktM), MemberType = typeof(Samples))]
    [MemberData(nameof(Samples.WktZM), MemberType = typeof(Samples))]
    public void Read_matches_nts(string wkt)
    {
        var expected = _nts.Read(wkt);
        var actual = _naive.Read(wkt);
        CoordinateAsserts.AssertCoordinatesBitEqual(expected, actual);
    }

    [Fact]
    public void Empty_geometries()
    {
        foreach (var wkt in new[] {
            "POINT EMPTY", "LINESTRING EMPTY", "POLYGON EMPTY",
            "MULTIPOINT EMPTY", "MULTILINESTRING EMPTY", "MULTIPOLYGON EMPTY",
            "GEOMETRYCOLLECTION EMPTY",
        })
        {
            var expected = _nts.Read(wkt);
            var actual = _naive.Read(wkt);
            Assert.True(actual.IsEmpty, $"{wkt}: actual is not empty");
            Assert.Equal(expected.GeometryType, actual.GeometryType);
        }
    }
}
