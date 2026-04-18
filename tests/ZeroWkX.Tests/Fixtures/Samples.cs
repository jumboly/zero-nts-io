using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;

namespace ZeroWkX.Tests.Fixtures;

public static class Samples
{
    public static readonly NtsGeometryServices Services = new(PackedCoordinateSequenceFactory.DoubleFactory);
    public static readonly GeometryFactory Factory = Services.CreateGeometryFactory();

    public static IEnumerable<object[]> WktAll2D()
    {
        yield return new object[] { "POINT (1 2)" };
        yield return new object[] { "POINT (1.5 -2.75)" };
        yield return new object[] { "LINESTRING (0 0, 1 1, 2 4, -3 2)" };
        yield return new object[] { "POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))" };
        yield return new object[] { "POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0), (2 2, 4 2, 4 4, 2 4, 2 2))" };
        yield return new object[] { "MULTIPOINT ((1 2), (3 4), (5 6))" };
        yield return new object[] { "MULTILINESTRING ((0 0, 1 1), (2 2, 3 3, 4 4))" };
        yield return new object[] { "MULTIPOLYGON (((0 0, 1 0, 1 1, 0 0)), ((5 5, 6 5, 6 6, 5 5)))" };
        yield return new object[] { "GEOMETRYCOLLECTION (POINT (1 2), LINESTRING (0 0, 1 1))" };
    }

    public static IEnumerable<object[]> WktZ()
    {
        yield return new object[] { "POINT Z (1 2 3)" };
        yield return new object[] { "LINESTRING Z (0 0 0, 1 1 1, 2 4 2)" };
        yield return new object[] { "POLYGON Z ((0 0 0, 10 0 1, 10 10 2, 0 10 3, 0 0 0))" };
    }

    public static IEnumerable<object[]> WktM()
    {
        yield return new object[] { "POINT M (1 2 99)" };
        yield return new object[] { "LINESTRING M (0 0 100, 1 1 200)" };
    }

    public static IEnumerable<object[]> WktZM()
    {
        yield return new object[] { "POINT ZM (1 2 3 99)" };
        yield return new object[] { "LINESTRING ZM (0 0 0 10, 1 1 1 20, 2 2 2 30)" };
    }
}
