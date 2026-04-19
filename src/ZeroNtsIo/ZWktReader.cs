using System.Buffers;
using ZeroNtsIo.Internal;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace ZeroNtsIo;

/// <summary>
/// Fast WKT reader: writes parsed ordinates directly into a packed <c>double[]</c>
/// (no <see cref="Coordinate"/> struct intermediate) and hands the array to NTS via
/// <see cref="NetTopologySuite.Geometries.Implementation.PackedCoordinateSequenceFactory"/>.
/// Combines the span-based tokenizer, custom double parser, and array-pool scratch buffers.
/// </summary>
public sealed class ZWktReader
{
    private readonly GeometryFactory _factory;
    private readonly CoordinateSequenceFactory _seqFactory;

    public ZWktReader(NtsGeometryServices? services = null)
    {
        _factory = (services ?? NtsGeometryServices.Instance).CreateGeometryFactory();
        _seqFactory = _factory.CoordinateSequenceFactory;
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
            1 => ReadPoint(ref c, ord),
            2 => ReadLineString(ref c, ord),
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

    private Point ReadPoint(ref WktCursor c, Ordinates ord)
    {
        int dim = PackedSequenceBuilder.DimensionOf(ord);
        var packed = new double[dim];
        WriteOne(ref c, ord, packed, 0);
        var seq = PackedSequenceBuilder.Create(_seqFactory, packed, dim, PackedSequenceBuilder.MeasuresOf(ord));
        return _factory.CreatePoint(seq);
    }

    private LineString ReadLineString(ref WktCursor c, Ordinates ord)
    {
        var seq = ReadSequence(ref c, ord);
        return _factory.CreateLineString(seq);
    }

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
                var seq = ReadSequence(ref c, ord);
                var ring = _factory.CreateLinearRing(seq);
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
                pts.Add(ReadPoint(ref c, ord));
                c.SkipWhitespace();
                c.Expect(')');
            }
            else pts.Add(ReadPoint(ref c, ord));
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
            lines.Add(_factory.CreateLineString(ReadSequence(ref c, ord)));
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

    private CoordinateSequence ReadSequence(ref WktCursor c, Ordinates ord)
    {
        int dim = PackedSequenceBuilder.DimensionOf(ord);
        var buf = new PooledDoubleBuffer(initialCapacity: 32);
        try
        {
            while (true)
            {
                AppendOne(ref c, ord, ref buf);
                if (!c.TryConsume(',')) break;
            }
            var packed = buf.Finish();
            return PackedSequenceBuilder.Create(_seqFactory, packed, dim, PackedSequenceBuilder.MeasuresOf(ord));
        }
        catch
        {
            buf.Dispose();
            throw;
        }
    }

    private static void AppendOne(ref WktCursor c, Ordinates ord, ref PooledDoubleBuffer buf)
    {
        double x = FastDoubleParser.Parse(c.ReadNumberToken());
        double y = FastDoubleParser.Parse(c.ReadNumberToken());
        switch (ord)
        {
            case Ordinates.XY:
                buf.AppendXY(x, y); break;
            case Ordinates.XYZ:
                buf.AppendXYZ(x, y, FastDoubleParser.Parse(c.ReadNumberToken())); break;
            case Ordinates.XYM:
                // Why: packed XYM uses 3 slots where the third ordinate is M, not Z.
                buf.AppendXYZ(x, y, FastDoubleParser.Parse(c.ReadNumberToken())); break;
            case Ordinates.XYZM:
                buf.AppendXYZM(x, y, FastDoubleParser.Parse(c.ReadNumberToken()), FastDoubleParser.Parse(c.ReadNumberToken())); break;
        }
    }

    private static void WriteOne(ref WktCursor c, Ordinates ord, double[] dst, int offset)
    {
        double x = FastDoubleParser.Parse(c.ReadNumberToken());
        double y = FastDoubleParser.Parse(c.ReadNumberToken());
        dst[offset + 0] = x;
        dst[offset + 1] = y;
        switch (ord)
        {
            case Ordinates.XYZ:
                dst[offset + 2] = FastDoubleParser.Parse(c.ReadNumberToken()); break;
            case Ordinates.XYM:
                dst[offset + 2] = FastDoubleParser.Parse(c.ReadNumberToken()); break;
            case Ordinates.XYZM:
                dst[offset + 2] = FastDoubleParser.Parse(c.ReadNumberToken());
                dst[offset + 3] = FastDoubleParser.Parse(c.ReadNumberToken()); break;
        }
    }
}
