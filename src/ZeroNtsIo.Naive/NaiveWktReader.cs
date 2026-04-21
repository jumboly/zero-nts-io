using System.Globalization;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace ZeroNtsIo.Naive;

public sealed class NaiveWktReader
{
    private readonly GeometryFactory _factory;

    public NaiveWktReader(NtsGeometryServices? services = null)
    {
        _factory = (services ?? NtsGeometryServices.Instance).CreateGeometryFactory();
    }

    public Geometry Read(ReadOnlySpan<char> wkt) => Read(wkt.ToString());

    public Geometry Read(string wkt)
    {
        var cursor = new Cursor(wkt);
        var g = ReadGeometry(ref cursor);
        cursor.SkipWhitespace();
        if (!cursor.AtEnd) throw new FormatException($"Unexpected trailing content at pos {cursor.Pos}");
        return g;
    }

    private Geometry ReadGeometry(ref Cursor c)
    {
        c.SkipWhitespace();
        var kw = c.ReadWord().ToUpperInvariant();
        c.SkipWhitespace();
        var ordinates = ReadOrdinatesModifier(ref c);
        c.SkipWhitespace();

        if (c.TryConsumeWord("EMPTY"))
            return CreateEmpty(kw, ordinates);

        c.Expect('(');
        Geometry result = kw switch
        {
            "POINT" => ReadPoint(ref c, ordinates),
            "LINESTRING" => ReadLineString(ref c, ordinates),
            "POLYGON" => ReadPolygon(ref c, ordinates),
            "MULTIPOINT" => ReadMultiPoint(ref c, ordinates),
            "MULTILINESTRING" => ReadMultiLineString(ref c, ordinates),
            "MULTIPOLYGON" => ReadMultiPolygon(ref c, ordinates),
            "GEOMETRYCOLLECTION" => ReadGeometryCollection(ref c),
            _ => throw new FormatException($"Unknown geometry type: {kw}"),
        };
        c.SkipWhitespace();
        c.Expect(')');
        return result;
    }

    private Ordinates ReadOrdinatesModifier(ref Cursor c)
    {
        var saved = c.Pos;
        var word = c.PeekWord();
        if (string.Equals(word, "ZM", StringComparison.OrdinalIgnoreCase)) { c.ReadWord(); return Ordinates.XYZM; }
        if (string.Equals(word, "Z", StringComparison.OrdinalIgnoreCase)) { c.ReadWord(); return Ordinates.XYZ; }
        if (string.Equals(word, "M", StringComparison.OrdinalIgnoreCase)) { c.ReadWord(); return Ordinates.XYM; }
        c.Pos = saved;
        return Ordinates.XY;
    }

    private Geometry CreateEmpty(string kw, Ordinates ord) => kw switch
    {
        "POINT" => _factory.CreatePoint((Coordinate?)null),
        "LINESTRING" => _factory.CreateLineString(Array.Empty<Coordinate>()),
        "POLYGON" => _factory.CreatePolygon((LinearRing?)null),
        "MULTIPOINT" => _factory.CreateMultiPoint(Array.Empty<Point>()),
        "MULTILINESTRING" => _factory.CreateMultiLineString(Array.Empty<LineString>()),
        "MULTIPOLYGON" => _factory.CreateMultiPolygon(Array.Empty<Polygon>()),
        "GEOMETRYCOLLECTION" => _factory.CreateGeometryCollection(Array.Empty<Geometry>()),
        _ => throw new FormatException($"Unknown geometry type: {kw}"),
    };

    private Point ReadPoint(ref Cursor c, Ordinates ord)
    {
        var coord = ReadCoordinate(ref c, ord);
        return _factory.CreatePoint(coord);
    }

    private LineString ReadLineString(ref Cursor c, Ordinates ord)
    {
        var coords = ReadCoordinateList(ref c, ord);
        return _factory.CreateLineString(coords);
    }

    private Polygon ReadPolygon(ref Cursor c, Ordinates ord)
    {
        var rings = new List<LinearRing>();
        while (true)
        {
            c.SkipWhitespace();
            c.Expect('(');
            var coords = ReadCoordinateList(ref c, ord);
            c.SkipWhitespace();
            c.Expect(')');
            rings.Add(_factory.CreateLinearRing(coords));
            c.SkipWhitespace();
            if (!c.TryConsume(',')) break;
        }
        var shell = rings[0];
        var holes = rings.Count > 1 ? rings.GetRange(1, rings.Count - 1).ToArray() : Array.Empty<LinearRing>();
        return _factory.CreatePolygon(shell, holes);
    }

    private MultiPoint ReadMultiPoint(ref Cursor c, Ordinates ord)
    {
        var pts = new List<Point>();
        while (true)
        {
            c.SkipWhitespace();
            // MULTIPOINT は (1 2, 3 4) と ((1 2), (3 4)) の両形式を受け付ける
            if (c.TryConsume('('))
            {
                var coord = ReadCoordinate(ref c, ord);
                c.SkipWhitespace();
                c.Expect(')');
                pts.Add(_factory.CreatePoint(coord));
            }
            else
            {
                var coord = ReadCoordinate(ref c, ord);
                pts.Add(_factory.CreatePoint(coord));
            }
            c.SkipWhitespace();
            if (!c.TryConsume(',')) break;
        }
        return _factory.CreateMultiPoint(pts.ToArray());
    }

    private MultiLineString ReadMultiLineString(ref Cursor c, Ordinates ord)
    {
        var lines = new List<LineString>();
        while (true)
        {
            c.SkipWhitespace();
            c.Expect('(');
            var coords = ReadCoordinateList(ref c, ord);
            c.SkipWhitespace();
            c.Expect(')');
            lines.Add(_factory.CreateLineString(coords));
            c.SkipWhitespace();
            if (!c.TryConsume(',')) break;
        }
        return _factory.CreateMultiLineString(lines.ToArray());
    }

    private MultiPolygon ReadMultiPolygon(ref Cursor c, Ordinates ord)
    {
        var polys = new List<Polygon>();
        while (true)
        {
            c.SkipWhitespace();
            c.Expect('(');
            polys.Add(ReadPolygon(ref c, ord));
            c.SkipWhitespace();
            c.Expect(')');
            c.SkipWhitespace();
            if (!c.TryConsume(',')) break;
        }
        return _factory.CreateMultiPolygon(polys.ToArray());
    }

    private GeometryCollection ReadGeometryCollection(ref Cursor c)
    {
        var parts = new List<Geometry>();
        while (true)
        {
            c.SkipWhitespace();
            parts.Add(ReadGeometry(ref c));
            c.SkipWhitespace();
            if (!c.TryConsume(',')) break;
        }
        return _factory.CreateGeometryCollection(parts.ToArray());
    }

    private Coordinate[] ReadCoordinateList(ref Cursor c, Ordinates ord)
    {
        var list = new List<Coordinate>();
        while (true)
        {
            list.Add(ReadCoordinate(ref c, ord));
            c.SkipWhitespace();
            if (!c.TryConsume(',')) break;
        }
        return list.ToArray();
    }

    private static Coordinate ReadCoordinate(ref Cursor c, Ordinates ord)
    {
        c.SkipWhitespace();
        var x = ReadDouble(ref c);
        c.SkipWhitespace();
        var y = ReadDouble(ref c);
        return ord switch
        {
            Ordinates.XY => new Coordinate(x, y),
            Ordinates.XYZ => new CoordinateZ(x, y, ReadNext(ref c)),
            Ordinates.XYM => new CoordinateM(x, y, ReadNext(ref c)),
            Ordinates.XYZM => new CoordinateZM(x, y, ReadNext(ref c), ReadNext(ref c)),
            _ => new Coordinate(x, y),
        };
    }

    private static double ReadNext(ref Cursor c)
    {
        c.SkipWhitespace();
        return ReadDouble(ref c);
    }

    private static double ReadDouble(ref Cursor c)
    {
        int start = c.Pos;
        var s = c.Source;
        int i = start;
        if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
        // Why: NTS は NaN の M 座標や EMPTY POINT を NaN / Infinity リテラルで出力する。
        // double.Parse がそのまま解釈できるよう、英字で始まるトークンもそのまま取り込む（InvariantCulture は両方を受理する）。
        if (i < s.Length && char.IsLetter(s[i]))
        {
            while (i < s.Length && char.IsLetter(s[i])) i++;
        }
        else
        {
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-'))
            {
                if ((s[i] == '+' || s[i] == '-') && i > start && s[i - 1] != 'e' && s[i - 1] != 'E') break;
                i++;
            }
        }
        var token = s.Substring(start, i - start);
        c.Pos = i;
        return double.Parse(token, CultureInfo.InvariantCulture);
    }

    private struct Cursor
    {
        public readonly string Source;
        public int Pos;
        public Cursor(string s) { Source = s; Pos = 0; }
        public bool AtEnd => Pos >= Source.Length;

        public void SkipWhitespace()
        {
            while (Pos < Source.Length && char.IsWhiteSpace(Source[Pos])) Pos++;
        }

        public void Expect(char c)
        {
            SkipWhitespace();
            if (Pos >= Source.Length || Source[Pos] != c)
                throw new FormatException($"Expected '{c}' at pos {Pos}");
            Pos++;
        }

        public bool TryConsume(char c)
        {
            SkipWhitespace();
            if (Pos < Source.Length && Source[Pos] == c) { Pos++; return true; }
            return false;
        }

        public bool TryConsumeWord(string word)
        {
            SkipWhitespace();
            if (Pos + word.Length > Source.Length) return false;
            for (int i = 0; i < word.Length; i++)
                if (char.ToUpperInvariant(Source[Pos + i]) != word[i]) return false;
            int next = Pos + word.Length;
            if (next < Source.Length && (char.IsLetter(Source[next]) || char.IsDigit(Source[next]))) return false;
            Pos = next;
            return true;
        }

        public string ReadWord()
        {
            SkipWhitespace();
            int start = Pos;
            while (Pos < Source.Length && char.IsLetter(Source[Pos])) Pos++;
            return Source.Substring(start, Pos - start);
        }

        public string PeekWord()
        {
            int saved = Pos;
            var w = ReadWord();
            Pos = saved;
            return w;
        }
    }
}
