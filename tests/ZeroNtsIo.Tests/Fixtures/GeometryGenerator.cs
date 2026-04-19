using NetTopologySuite.Geometries;

namespace ZeroNtsIo.Tests.Fixtures;

/// <summary>
/// Seeded random geometry generator.
/// Every call with the same (kind, seed, ord, count) is bit-reproducible — required because
/// tests compare bit-for-bit against NTS output.
/// </summary>
public static class GeometryGenerator
{
    private static readonly GeometryFactory Factory = Samples.Factory;

    public static Geometry Build(string kind, Ordinates ord, int coords, int seed)
    {
        var rand = new Random(seed);
        return kind switch
        {
            "Point" => Factory.CreatePoint(MakeCoord(rand, ord)),
            "LineString" => Factory.CreateLineString(MakeCoordArray(rand, ord, Math.Max(2, coords))),
            "Polygon" => Factory.CreatePolygon(Factory.CreateLinearRing(MakeRing(rand, ord, coords))),
            "PolygonWithHoles" => MakePolygonHoles(rand, ord, coords),
            "MultiPoint" => Factory.CreateMultiPoint(MakePoints(rand, ord, Math.Max(1, coords / 4))),
            "MultiLineString" => Factory.CreateMultiLineString(MakeLineStrings(rand, ord, coords)),
            "MultiPolygon" => Factory.CreateMultiPolygon(MakePolygons(rand, ord, coords)),
            "MultiPolygonWithHoles" => Factory.CreateMultiPolygon(MakePolygonsWithHoles(rand, ord, coords)),
            "GeometryCollection" => MakeCollection(rand, ord, coords),
            "NestedCollection" => MakeNested(rand, ord, coords),
            _ => throw new ArgumentException(kind),
        };
    }

    public static IEnumerable<object[]> CombinationMatrix()
    {
        // Why: picked so every reader touches (a) short/long paths, (b) all ordinate layouts,
        // (c) every top-level geometry type, including the recursive GeometryCollection cases.
        string[] kinds =
        [
            "Point", "LineString", "Polygon", "PolygonWithHoles",
            "MultiPoint", "MultiLineString", "MultiPolygon", "MultiPolygonWithHoles",
            "GeometryCollection", "NestedCollection",
        ];
        Ordinates[] ordinates = [Ordinates.XY, Ordinates.XYZ, Ordinates.XYM, Ordinates.XYZM];
        int[] coordCounts = [4, 32, 256];

        foreach (var kind in kinds)
            foreach (var ord in ordinates)
                foreach (var count in coordCounts)
                    yield return new object[] { kind, ord, count, 42 };
    }

    private static Coordinate MakeCoord(Random r, Ordinates ord)
    {
        double x = r.NextDouble() * 360 - 180;
        double y = r.NextDouble() * 180 - 90;
        return ord switch
        {
            Ordinates.XYZ => new CoordinateZ(x, y, r.NextDouble() * 1000),
            Ordinates.XYM => new CoordinateM(x, y, r.NextDouble() * 1000),
            Ordinates.XYZM => new CoordinateZM(x, y, r.NextDouble() * 1000, r.NextDouble() * 1000),
            _ => new Coordinate(x, y),
        };
    }

    private static Coordinate[] MakeCoordArray(Random r, Ordinates ord, int n)
    {
        var a = new Coordinate[n];
        for (int i = 0; i < n; i++) a[i] = MakeCoord(r, ord);
        return a;
    }

    private static Coordinate[] MakeRing(Random r, Ordinates ord, int n)
    {
        if (n < 4) n = 4;
        var ring = new Coordinate[n];
        for (int i = 0; i < n - 1; i++) ring[i] = MakeCoord(r, ord);
        ring[n - 1] = (Coordinate)ring[0].Copy();
        return ring;
    }

    private static Polygon MakePolygonHoles(Random r, Ordinates ord, int coords)
    {
        int outer = Math.Max(4, coords / 2);
        int holes = 2;
        int each = Math.Max(4, (coords - outer) / holes);
        var shell = Factory.CreateLinearRing(MakeRing(r, ord, outer));
        var inner = new LinearRing[holes];
        for (int i = 0; i < holes; i++) inner[i] = Factory.CreateLinearRing(MakeRing(r, ord, each));
        return Factory.CreatePolygon(shell, inner);
    }

    private static Point[] MakePoints(Random r, Ordinates ord, int n)
    {
        var pts = new Point[n];
        for (int i = 0; i < n; i++) pts[i] = Factory.CreatePoint(MakeCoord(r, ord));
        return pts;
    }

    private static LineString[] MakeLineStrings(Random r, Ordinates ord, int coords)
    {
        int parts = 3;
        int each = Math.Max(2, coords / parts);
        var lines = new LineString[parts];
        for (int i = 0; i < parts; i++)
            lines[i] = Factory.CreateLineString(MakeCoordArray(r, ord, each));
        return lines;
    }

    private static Polygon[] MakePolygons(Random r, Ordinates ord, int coords)
    {
        int parts = 3;
        int each = Math.Max(4, coords / parts);
        var polys = new Polygon[parts];
        for (int i = 0; i < parts; i++)
            polys[i] = Factory.CreatePolygon(Factory.CreateLinearRing(MakeRing(r, ord, each)));
        return polys;
    }

    private static Polygon[] MakePolygonsWithHoles(Random r, Ordinates ord, int coords)
    {
        int parts = 3;
        int each = Math.Max(4, coords / parts);
        var polys = new Polygon[parts];
        for (int i = 0; i < parts; i++)
            polys[i] = MakePolygonHoles(r, ord, each);
        return polys;
    }

    private static GeometryCollection MakeCollection(Random r, Ordinates ord, int coords)
    {
        int each = Math.Max(4, coords / 3);
        return Factory.CreateGeometryCollection(new Geometry[]
        {
            Factory.CreatePoint(MakeCoord(r, ord)),
            Factory.CreateLineString(MakeCoordArray(r, ord, each)),
            Factory.CreatePolygon(Factory.CreateLinearRing(MakeRing(r, ord, each))),
        });
    }

    private static GeometryCollection MakeNested(Random r, Ordinates ord, int coords)
    {
        // Why: nested GeometryCollection is spec-legal but trips implementations that don't recurse.
        int each = Math.Max(4, coords / 4);
        var inner = Factory.CreateGeometryCollection(new Geometry[]
        {
            Factory.CreateLineString(MakeCoordArray(r, ord, each)),
            Factory.CreatePoint(MakeCoord(r, ord)),
        });
        return Factory.CreateGeometryCollection(new Geometry[]
        {
            Factory.CreatePoint(MakeCoord(r, ord)),
            inner,
            Factory.CreatePolygon(Factory.CreateLinearRing(MakeRing(r, ord, each))),
        });
    }
}
