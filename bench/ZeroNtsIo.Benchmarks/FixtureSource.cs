using ZeroNtsIo.Reference;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NetTopologySuite.IO;

namespace ZeroNtsIo.Benchmarks;

public static class FixtureSource
{
    public static readonly NtsGeometryServices Services = new(PackedCoordinateSequenceFactory.DoubleFactory);
    private static readonly NtsWktWriter NtsWktW = new();
    private static readonly NtsWkbWriter NtsWkbW = new();

    public static Geometry BuildGeometry(string kind, int coords, int seed, Ordinates ord = Ordinates.XY)
    {
        var rand = new Random(seed);
        var factory = Services.CreateGeometryFactory();
        return kind switch
        {
            "Point" => factory.CreatePoint(MakeCoord(rand, ord)),
            "MultiPoint" => factory.CreateMultiPointFromCoords(GenerateCoords(rand, coords, ord)),
            "LineString" => factory.CreateLineString(GenerateCoords(rand, Math.Max(2, coords), ord)),
            "Polygon" => factory.CreatePolygon(factory.CreateLinearRing(GenerateRing(rand, coords, ord))),
            "PolygonWithHoles" => BuildPolygonWithHoles(rand, factory, coords, ord),
            "MultiPolygon" => factory.CreateMultiPolygon(GeneratePolygons(rand, factory, coords, ord)),
            "GeometryCollection" => BuildGeometryCollection(rand, factory, coords, ord),
            _ => throw new ArgumentException(kind),
        };
    }

    public static string BuildWkt(string kind, int coords, int seed, Ordinates ord = Ordinates.XY) =>
        NtsWktW.Write(BuildGeometry(kind, coords, seed, ord));

    public static byte[] BuildWkb(string kind, int coords, int seed, ByteOrder order, Ordinates ord = Ordinates.XY) =>
        NtsWkbW.Write(BuildGeometry(kind, coords, seed, ord), order);

    /// <summary>
    /// Build a deterministically-seeded "realistic-shape" MultiPolygon approximating the statistical
    /// profile of country/coastline geodata — varying ring sizes, multiple holes, and dense near-
    /// collinear segments that the uniform-random generator does not produce.
    /// </summary>
    public static Geometry BuildRealisticShape(int seed)
    {
        var rand = new Random(seed);
        var factory = Services.CreateGeometryFactory();

        // Why: three polygons at different scales mimic a mainland + midsize island + small island.
        var mainland = BuildDenseRingedPolygon(rand, factory, outer: 5_000, holes: 3, holeSize: 250);
        var island = BuildDenseRingedPolygon(rand, factory, outer: 1_500, holes: 1, holeSize: 80);
        var islet = BuildDenseRingedPolygon(rand, factory, outer: 120, holes: 0, holeSize: 0);
        return factory.CreateMultiPolygon(new[] { mainland, island, islet });
    }

    public static byte[] BuildRealisticWkb(ByteOrder order, int seed = 42) =>
        NtsWkbW.Write(BuildRealisticShape(seed), order);

    public static string BuildRealisticWkt(int seed = 42) =>
        NtsWktW.Write(BuildRealisticShape(seed));

    private static Polygon BuildDenseRingedPolygon(Random r, GeometryFactory f, int outer, int holes, int holeSize)
    {
        var shell = f.CreateLinearRing(MakeDenseRing(r, outer));
        if (holes == 0) return f.CreatePolygon(shell);
        var inner = new LinearRing[holes];
        for (int i = 0; i < holes; i++) inner[i] = f.CreateLinearRing(MakeDenseRing(r, holeSize));
        return f.CreatePolygon(shell, inner);
    }

    /// <summary>
    /// Generate a polygonal ring whose vertex density follows a parametric curve plus small jitter —
    /// yielding near-collinear runs (characteristic of GPS-traced boundaries) that pure uniform
    /// random does not produce.
    /// </summary>
    private static Coordinate[] MakeDenseRing(Random r, int n)
    {
        if (n < 4) n = 4;
        var ring = new Coordinate[n];
        double cx = r.NextDouble() * 360 - 180;
        double cy = r.NextDouble() * 180 - 90;
        double baseR = 0.01 + r.NextDouble() * 5;
        // Radial-harmonic distortion to mimic a coastline shape.
        double a1 = r.NextDouble() * 0.3;
        double a2 = r.NextDouble() * 0.15;
        int k1 = r.Next(3, 9);
        int k2 = r.Next(2, 6);
        for (int i = 0; i < n - 1; i++)
        {
            double t = (double)i / (n - 1) * 2 * Math.PI;
            double rad = baseR * (1 + a1 * Math.Sin(k1 * t) + a2 * Math.Cos(k2 * t));
            // Small jitter keeps the GPS-like near-collinear character without degenerate dupes.
            double jitter = (r.NextDouble() - 0.5) * baseR * 0.001;
            ring[i] = new Coordinate(cx + (rad + jitter) * Math.Cos(t), cy + (rad + jitter) * Math.Sin(t));
        }
        ring[n - 1] = (Coordinate)ring[0].Copy();
        return ring;
    }

    private static Coordinate MakeCoord(Random rand, Ordinates ord)
    {
        double x = rand.NextDouble() * 360 - 180;
        double y = rand.NextDouble() * 180 - 90;
        return ord switch
        {
            Ordinates.XY => new Coordinate(x, y),
            Ordinates.XYZ => new CoordinateZ(x, y, rand.NextDouble() * 1000),
            Ordinates.XYM => new CoordinateM(x, y, rand.NextDouble() * 1000),
            Ordinates.XYZM => new CoordinateZM(x, y, rand.NextDouble() * 1000, rand.NextDouble() * 1000),
            _ => new Coordinate(x, y),
        };
    }

    private static Coordinate[] GenerateCoords(Random rand, int n, Ordinates ord)
    {
        var arr = new Coordinate[n];
        for (int i = 0; i < n; i++) arr[i] = MakeCoord(rand, ord);
        return arr;
    }

    private static Coordinate[] GenerateRing(Random rand, int n, Ordinates ord)
    {
        // Why: LinearRing requires >= 4 distinct points and first == last.
        if (n < 4) n = 4;
        var ring = new Coordinate[n];
        for (int i = 0; i < n - 1; i++) ring[i] = MakeCoord(rand, ord);
        ring[n - 1] = (Coordinate)ring[0].Copy();
        return ring;
    }

    private static Polygon BuildPolygonWithHoles(Random rand, GeometryFactory factory, int totalCoords, Ordinates ord)
    {
        // Why: split the budget so the outer ring gets half and three interior rings share the rest.
        // This exercises the per-ring loop (V3's ArrayPool<LinearRing>) on non-trivial hole counts.
        int holes = 3;
        int outer = Math.Max(4, totalCoords / 2);
        int perHole = Math.Max(4, (totalCoords - outer) / holes);
        var shell = factory.CreateLinearRing(GenerateRing(rand, outer, ord));
        var rings = new LinearRing[holes];
        for (int i = 0; i < holes; i++) rings[i] = factory.CreateLinearRing(GenerateRing(rand, perHole, ord));
        return factory.CreatePolygon(shell, rings);
    }

    private static Polygon[] GeneratePolygons(Random rand, GeometryFactory factory, int totalCoords, Ordinates ord)
    {
        int parts = 4;
        int each = Math.Max(4, totalCoords / parts);
        var polys = new Polygon[parts];
        for (int i = 0; i < parts; i++)
            polys[i] = factory.CreatePolygon(factory.CreateLinearRing(GenerateRing(rand, each, ord)));
        return polys;
    }

    private static GeometryCollection BuildGeometryCollection(Random rand, GeometryFactory factory, int totalCoords, Ordinates ord)
    {
        // Why: a realistic mix — one big LineString, one polygon, a handful of points — so parsers
        // hit the recursive GeometryCollection path with varied child types.
        int lsCoords = Math.Max(4, totalCoords / 2);
        int polyCoords = Math.Max(4, totalCoords / 4);
        int pointCount = Math.Max(1, totalCoords / 32);

        var parts = new List<Geometry>(2 + pointCount)
        {
            factory.CreateLineString(GenerateCoords(rand, lsCoords, ord)),
            factory.CreatePolygon(factory.CreateLinearRing(GenerateRing(rand, polyCoords, ord))),
        };
        for (int i = 0; i < pointCount; i++)
            parts.Add(factory.CreatePoint(MakeCoord(rand, ord)));
        return factory.CreateGeometryCollection(parts.ToArray());
    }
}
