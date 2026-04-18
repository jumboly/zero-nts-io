using System.Buffers.Binary;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace ZeroWkX.Naive;

public sealed class NaiveWkbReader
{
    private readonly GeometryFactory _factory;

    public NaiveWkbReader(NtsGeometryServices? services = null)
    {
        _factory = (services ?? NtsGeometryServices.Instance).CreateGeometryFactory();
    }

    public Geometry Read(ReadOnlySpan<byte> wkb) => Read(wkb.ToArray());

    public Geometry Read(byte[] wkb)
    {
        using var ms = new MemoryStream(wkb);
        using var br = new BinaryReader(ms);
        return ReadGeometry(br);
    }

    private Geometry ReadGeometry(BinaryReader br)
    {
        byte bo = br.ReadByte();
        if (bo != 0 && bo != 1) throw new FormatException($"Invalid byte order: {bo}");
        bool le = bo == 1;

        uint rawType = ReadUInt32(br, le);
        // Why: reject EWKB. OGC ISO uses only the low bits (1..7) plus a 1000/2000/3000 offset.
        if ((rawType & 0xE0000000u) != 0)
            throw new FormatException("EWKB (PostGIS SRID/Z/M high-bit flags) is not supported; use OGC ISO WKB.");

        uint ordCode = rawType / 1000u;   // 0=XY, 1=XYZ, 2=XYM, 3=XYZM
        uint baseType = rawType % 1000u;
        var ord = ordCode switch
        {
            0 => Ordinates.XY,
            1 => Ordinates.XYZ,
            2 => Ordinates.XYM,
            3 => Ordinates.XYZM,
            _ => throw new FormatException($"Unknown ordinate code: {ordCode}"),
        };
        int dim = ord switch { Ordinates.XY => 2, Ordinates.XYZ => 3, Ordinates.XYM => 3, Ordinates.XYZM => 4, _ => 2 };

        return baseType switch
        {
            1 => ReadPoint(br, le, ord, dim),
            2 => ReadLineString(br, le, ord, dim),
            3 => ReadPolygon(br, le, ord, dim),
            4 => ReadMultiPoint(br, le),
            5 => ReadMultiLineString(br, le),
            6 => ReadMultiPolygon(br, le),
            7 => ReadGeometryCollection(br, le),
            _ => throw new FormatException($"Unknown WKB geometry type: {baseType}"),
        };
    }

    private Point ReadPoint(BinaryReader br, bool le, Ordinates ord, int dim)
    {
        var coord = ReadCoord(br, le, ord);
        // Why: OGC encodes POINT EMPTY as all-NaN ordinates.
        if (double.IsNaN(coord.X) && double.IsNaN(coord.Y))
            return _factory.CreatePoint((Coordinate?)null);
        return _factory.CreatePoint(coord);
    }

    private LineString ReadLineString(BinaryReader br, bool le, Ordinates ord, int dim)
    {
        int n = (int)ReadUInt32(br, le);
        var coords = new Coordinate[n];
        for (int i = 0; i < n; i++) coords[i] = ReadCoord(br, le, ord);
        return _factory.CreateLineString(coords);
    }

    private Polygon ReadPolygon(BinaryReader br, bool le, Ordinates ord, int dim)
    {
        int nRings = (int)ReadUInt32(br, le);
        if (nRings == 0) return _factory.CreatePolygon((LinearRing?)null);
        var rings = new LinearRing[nRings];
        for (int r = 0; r < nRings; r++)
        {
            int n = (int)ReadUInt32(br, le);
            var coords = new Coordinate[n];
            for (int i = 0; i < n; i++) coords[i] = ReadCoord(br, le, ord);
            rings[r] = _factory.CreateLinearRing(coords);
        }
        var holes = nRings > 1 ? rings.AsSpan(1).ToArray() : Array.Empty<LinearRing>();
        return _factory.CreatePolygon(rings[0], holes);
    }

    private MultiPoint ReadMultiPoint(BinaryReader br, bool le)
    {
        int n = (int)ReadUInt32(br, le);
        var pts = new Point[n];
        for (int i = 0; i < n; i++) pts[i] = (Point)ReadGeometry(br);
        return _factory.CreateMultiPoint(pts);
    }

    private MultiLineString ReadMultiLineString(BinaryReader br, bool le)
    {
        int n = (int)ReadUInt32(br, le);
        var lines = new LineString[n];
        for (int i = 0; i < n; i++) lines[i] = (LineString)ReadGeometry(br);
        return _factory.CreateMultiLineString(lines);
    }

    private MultiPolygon ReadMultiPolygon(BinaryReader br, bool le)
    {
        int n = (int)ReadUInt32(br, le);
        var polys = new Polygon[n];
        for (int i = 0; i < n; i++) polys[i] = (Polygon)ReadGeometry(br);
        return _factory.CreateMultiPolygon(polys);
    }

    private GeometryCollection ReadGeometryCollection(BinaryReader br, bool le)
    {
        int n = (int)ReadUInt32(br, le);
        var parts = new Geometry[n];
        for (int i = 0; i < n; i++) parts[i] = ReadGeometry(br);
        return _factory.CreateGeometryCollection(parts);
    }

    private static Coordinate ReadCoord(BinaryReader br, bool le, Ordinates ord)
    {
        double x = ReadDouble(br, le);
        double y = ReadDouble(br, le);
        return ord switch
        {
            Ordinates.XY => new Coordinate(x, y),
            Ordinates.XYZ => new CoordinateZ(x, y, ReadDouble(br, le)),
            Ordinates.XYM => new CoordinateM(x, y, ReadDouble(br, le)),
            Ordinates.XYZM => new CoordinateZM(x, y, ReadDouble(br, le), ReadDouble(br, le)),
            _ => new Coordinate(x, y),
        };
    }

    private static uint ReadUInt32(BinaryReader br, bool le)
    {
        Span<byte> buf = stackalloc byte[4];
        if (br.Read(buf) != 4) throw new EndOfStreamException();
        return le ? BinaryPrimitives.ReadUInt32LittleEndian(buf) : BinaryPrimitives.ReadUInt32BigEndian(buf);
    }

    private static double ReadDouble(BinaryReader br, bool le)
    {
        Span<byte> buf = stackalloc byte[8];
        if (br.Read(buf) != 8) throw new EndOfStreamException();
        return le ? BinaryPrimitives.ReadDoubleLittleEndian(buf) : BinaryPrimitives.ReadDoubleBigEndian(buf);
    }
}
