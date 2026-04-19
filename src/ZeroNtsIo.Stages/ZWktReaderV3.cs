using System.Buffers;
using ZeroNtsIo.Stages.Internal;
using ZeroNtsIo.Internal;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace ZeroNtsIo.Stages;

/// <summary>V3: V2 + <see cref="ArrayPool{T}"/> for coordinate-list growth and per-geometry arrays.</summary>
public sealed class ZWktReaderV3
{
    private readonly GeometryFactory _factory;

    public ZWktReaderV3(NtsGeometryServices? services = null)
    {
        _factory = (services ?? NtsGeometryServices.Instance).CreateGeometryFactory();
    }

    public Geometry Read(string wkt) => Read(wkt.AsSpan());

    public Geometry Read(ReadOnlySpan<char> wkt)
    {
        var c = new WktCursor(wkt);
        var g = ReadGeometry(ref c);
        c.SkipWhitespace();
        if (!c.AtEnd) throw new FormatException($"Unexpected trailing content at pos {c.Pos}");
        return g;
    }

    private Geometry ReadGeometry(ref WktCursor c)
    {
        c.SkipWhitespace();
        int kw = ReadTypeKeyword(ref c);
        var ord = ReadOrdinatesModifier(ref c);

        if (c.TryConsumeKeyword("EMPTY")) return CreateEmpty(kw);

        c.Expect('(');
        Geometry result = kw switch
        {
            1 => _factory.CreatePoint(ReadCoordinate(ref c, ord)),
            2 => _factory.CreateLineString(ReadCoordinateList(ref c, ord)),
            3 => ReadPolygon(ref c, ord),
            4 => ReadMultiPoint(ref c, ord),
            5 => ReadMultiLineString(ref c, ord),
            6 => ReadMultiPolygon(ref c, ord),
            7 => ReadGeometryCollection(ref c),
            _ => throw new FormatException("Unknown geometry type"),
        };
        c.SkipWhitespace();
        c.Expect(')');
        return result;
    }

    private static int ReadTypeKeyword(ref WktCursor c)
    {
        c.SkipWhitespace();
        var w = c.ReadWord();
        if (w.Equals("POINT", StringComparison.OrdinalIgnoreCase)) return 1;
        if (w.Equals("LINESTRING", StringComparison.OrdinalIgnoreCase)) return 2;
        if (w.Equals("POLYGON", StringComparison.OrdinalIgnoreCase)) return 3;
        if (w.Equals("MULTIPOINT", StringComparison.OrdinalIgnoreCase)) return 4;
        if (w.Equals("MULTILINESTRING", StringComparison.OrdinalIgnoreCase)) return 5;
        if (w.Equals("MULTIPOLYGON", StringComparison.OrdinalIgnoreCase)) return 6;
        if (w.Equals("GEOMETRYCOLLECTION", StringComparison.OrdinalIgnoreCase)) return 7;
        throw new FormatException($"Unknown geometry keyword: {w.ToString()}");
    }

    private static Ordinates ReadOrdinatesModifier(ref WktCursor c)
    {
        var saved = c.Pos;
        var w = c.ReadWord();
        if (w.Equals("ZM", StringComparison.OrdinalIgnoreCase)) return Ordinates.XYZM;
        if (w.Equals("Z", StringComparison.OrdinalIgnoreCase)) return Ordinates.XYZ;
        if (w.Equals("M", StringComparison.OrdinalIgnoreCase)) return Ordinates.XYM;
        c.Pos = saved;
        return Ordinates.XY;
    }

    private Geometry CreateEmpty(int kw) => kw switch
    {
        1 => _factory.CreatePoint((Coordinate?)null),
        2 => _factory.CreateLineString(Array.Empty<Coordinate>()),
        3 => _factory.CreatePolygon((LinearRing?)null),
        4 => _factory.CreateMultiPoint(Array.Empty<Point>()),
        5 => _factory.CreateMultiLineString(Array.Empty<LineString>()),
        6 => _factory.CreateMultiPolygon(Array.Empty<Polygon>()),
        7 => _factory.CreateGeometryCollection(Array.Empty<Geometry>()),
        _ => throw new FormatException(),
    };

    private Polygon ReadPolygon(ref WktCursor c, Ordinates ord)
    {
        var ringsBuf = ArrayPool<LinearRing>.Shared.Rent(4);
        int ringCount = 0;
        try
        {
            while (true)
            {
                c.SkipWhitespace();
                c.Expect('(');
                var ring = _factory.CreateLinearRing(ReadCoordinateList(ref c, ord));
                if (ringCount == ringsBuf.Length)
                {
                    var bigger = ArrayPool<LinearRing>.Shared.Rent(ringsBuf.Length * 2);
                    Array.Copy(ringsBuf, bigger, ringCount);
                    ArrayPool<LinearRing>.Shared.Return(ringsBuf, clearArray: true);
                    ringsBuf = bigger;
                }
                ringsBuf[ringCount++] = ring;
                c.SkipWhitespace();
                c.Expect(')');
                if (!c.TryConsume(',')) break;
            }
            var shell = ringsBuf[0];
            LinearRing[] holes = ringCount > 1 ? new LinearRing[ringCount - 1] : Array.Empty<LinearRing>();
            for (int i = 1; i < ringCount; i++) holes[i - 1] = ringsBuf[i];
            return _factory.CreatePolygon(shell, holes);
        }
        finally
        {
            ArrayPool<LinearRing>.Shared.Return(ringsBuf, clearArray: true);
        }
    }

    private MultiPoint ReadMultiPoint(ref WktCursor c, Ordinates ord)
    {
        var pts = new List<Point>(8);
        while (true)
        {
            c.SkipWhitespace();
            if (c.TryConsume('('))
            {
                pts.Add(_factory.CreatePoint(ReadCoordinate(ref c, ord)));
                c.SkipWhitespace();
                c.Expect(')');
            }
            else pts.Add(_factory.CreatePoint(ReadCoordinate(ref c, ord)));
            if (!c.TryConsume(',')) break;
        }
        return _factory.CreateMultiPoint(pts.ToArray());
    }

    private MultiLineString ReadMultiLineString(ref WktCursor c, Ordinates ord)
    {
        var lines = new List<LineString>(4);
        while (true)
        {
            c.SkipWhitespace();
            c.Expect('(');
            lines.Add(_factory.CreateLineString(ReadCoordinateList(ref c, ord)));
            c.SkipWhitespace();
            c.Expect(')');
            if (!c.TryConsume(',')) break;
        }
        return _factory.CreateMultiLineString(lines.ToArray());
    }

    private MultiPolygon ReadMultiPolygon(ref WktCursor c, Ordinates ord)
    {
        var polys = new List<Polygon>(4);
        while (true)
        {
            c.SkipWhitespace();
            c.Expect('(');
            polys.Add(ReadPolygon(ref c, ord));
            c.SkipWhitespace();
            c.Expect(')');
            if (!c.TryConsume(',')) break;
        }
        return _factory.CreateMultiPolygon(polys.ToArray());
    }

    private GeometryCollection ReadGeometryCollection(ref WktCursor c)
    {
        var parts = new List<Geometry>(4);
        while (true)
        {
            parts.Add(ReadGeometry(ref c));
            if (!c.TryConsume(',')) break;
        }
        return _factory.CreateGeometryCollection(parts.ToArray());
    }

    private Coordinate[] ReadCoordinateList(ref WktCursor c, Ordinates ord)
    {
        using var buf = new PooledCoordinateBuffer(initialCapacity: 16);
        while (true)
        {
            buf.Add(ReadCoordinate(ref c, ord));
            if (!c.TryConsume(',')) break;
        }
        return buf.Finish();
    }

    private static Coordinate ReadCoordinate(ref WktCursor c, Ordinates ord)
    {
        double x = FastDoubleParser.Parse(c.ReadNumberToken());
        double y = FastDoubleParser.Parse(c.ReadNumberToken());
        return ord switch
        {
            Ordinates.XY => new Coordinate(x, y),
            Ordinates.XYZ => new CoordinateZ(x, y, FastDoubleParser.Parse(c.ReadNumberToken())),
            Ordinates.XYM => new CoordinateM(x, y, FastDoubleParser.Parse(c.ReadNumberToken())),
            Ordinates.XYZM => new CoordinateZM(x, y, FastDoubleParser.Parse(c.ReadNumberToken()), FastDoubleParser.Parse(c.ReadNumberToken())),
            _ => new Coordinate(x, y),
        };
    }
}
