using System.Buffers.Binary;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace ZeroNtsIo.Stages;

/// <summary>V1: Span-based WKB parsing, no BinaryReader/MemoryStream allocs.</summary>
public sealed class ZWkbReaderV1
{
    private readonly GeometryFactory _factory;

    public ZWkbReaderV1(NtsGeometryServices? services = null)
    {
        _factory = (services ?? NtsGeometryServices.Instance).CreateGeometryFactory();
    }

    public Geometry Read(byte[] wkb) => Read(wkb.AsSpan());

    public Geometry Read(ReadOnlySpan<byte> wkb)
    {
        int pos = 0;
        return ReadGeometry(wkb, ref pos);
    }

    private Geometry ReadGeometry(ReadOnlySpan<byte> buf, ref int pos)
    {
        byte bo = buf[pos++];
        if (bo != 0 && bo != 1) throw new FormatException($"Invalid byte order: {bo}");
        bool le = bo == 1;

        uint rawType = ReadUInt32(buf, ref pos, le);
        if ((rawType & 0xE0000000u) != 0)
            throw new FormatException("EWKB (PostGIS SRID/Z/M high-bit flags) is not supported; use OGC ISO WKB.");

        uint ordCode = rawType / 1000u;
        uint baseType = rawType % 1000u;
        var ord = ordCode switch
        {
            0 => Ordinates.XY,
            1 => Ordinates.XYZ,
            2 => Ordinates.XYM,
            3 => Ordinates.XYZM,
            _ => throw new FormatException($"Unknown ordinate code: {ordCode}"),
        };

        return baseType switch
        {
            1 => ReadPoint(buf, ref pos, le, ord),
            2 => ReadLineString(buf, ref pos, le, ord),
            3 => ReadPolygon(buf, ref pos, le, ord),
            4 => ReadMultiPoint(buf, ref pos, le),
            5 => ReadMultiLineString(buf, ref pos, le),
            6 => ReadMultiPolygon(buf, ref pos, le),
            7 => ReadGeometryCollection(buf, ref pos, le),
            _ => throw new FormatException($"Unknown WKB geometry type: {baseType}"),
        };
    }

    private Point ReadPoint(ReadOnlySpan<byte> buf, ref int pos, bool le, Ordinates ord)
    {
        var coord = ReadCoord(buf, ref pos, le, ord);
        if (double.IsNaN(coord.X) && double.IsNaN(coord.Y))
            return _factory.CreatePoint((Coordinate?)null);
        return _factory.CreatePoint(coord);
    }

    private LineString ReadLineString(ReadOnlySpan<byte> buf, ref int pos, bool le, Ordinates ord)
    {
        int n = (int)ReadUInt32(buf, ref pos, le);
        var coords = new Coordinate[n];
        for (int i = 0; i < n; i++) coords[i] = ReadCoord(buf, ref pos, le, ord);
        return _factory.CreateLineString(coords);
    }

    private Polygon ReadPolygon(ReadOnlySpan<byte> buf, ref int pos, bool le, Ordinates ord)
    {
        int nRings = (int)ReadUInt32(buf, ref pos, le);
        if (nRings == 0) return _factory.CreatePolygon((LinearRing?)null);
        var rings = new LinearRing[nRings];
        for (int r = 0; r < nRings; r++)
        {
            int n = (int)ReadUInt32(buf, ref pos, le);
            var coords = new Coordinate[n];
            for (int i = 0; i < n; i++) coords[i] = ReadCoord(buf, ref pos, le, ord);
            rings[r] = _factory.CreateLinearRing(coords);
        }
        var holes = nRings > 1 ? rings.AsSpan(1).ToArray() : Array.Empty<LinearRing>();
        return _factory.CreatePolygon(rings[0], holes);
    }

    private MultiPoint ReadMultiPoint(ReadOnlySpan<byte> buf, ref int pos, bool le)
    {
        int n = (int)ReadUInt32(buf, ref pos, le);
        var pts = new Point[n];
        for (int i = 0; i < n; i++) pts[i] = (Point)ReadGeometry(buf, ref pos);
        return _factory.CreateMultiPoint(pts);
    }

    private MultiLineString ReadMultiLineString(ReadOnlySpan<byte> buf, ref int pos, bool le)
    {
        int n = (int)ReadUInt32(buf, ref pos, le);
        var lines = new LineString[n];
        for (int i = 0; i < n; i++) lines[i] = (LineString)ReadGeometry(buf, ref pos);
        return _factory.CreateMultiLineString(lines);
    }

    private MultiPolygon ReadMultiPolygon(ReadOnlySpan<byte> buf, ref int pos, bool le)
    {
        int n = (int)ReadUInt32(buf, ref pos, le);
        var polys = new Polygon[n];
        for (int i = 0; i < n; i++) polys[i] = (Polygon)ReadGeometry(buf, ref pos);
        return _factory.CreateMultiPolygon(polys);
    }

    private GeometryCollection ReadGeometryCollection(ReadOnlySpan<byte> buf, ref int pos, bool le)
    {
        int n = (int)ReadUInt32(buf, ref pos, le);
        var parts = new Geometry[n];
        for (int i = 0; i < n; i++) parts[i] = ReadGeometry(buf, ref pos);
        return _factory.CreateGeometryCollection(parts);
    }

    private static Coordinate ReadCoord(ReadOnlySpan<byte> buf, ref int pos, bool le, Ordinates ord)
    {
        double x = ReadDouble(buf, ref pos, le);
        double y = ReadDouble(buf, ref pos, le);
        return ord switch
        {
            Ordinates.XY => new Coordinate(x, y),
            Ordinates.XYZ => new CoordinateZ(x, y, ReadDouble(buf, ref pos, le)),
            Ordinates.XYM => new CoordinateM(x, y, ReadDouble(buf, ref pos, le)),
            Ordinates.XYZM => new CoordinateZM(x, y, ReadDouble(buf, ref pos, le), ReadDouble(buf, ref pos, le)),
            _ => new Coordinate(x, y),
        };
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> buf, ref int pos, bool le)
    {
        var slice = buf.Slice(pos, 4);
        pos += 4;
        return le ? BinaryPrimitives.ReadUInt32LittleEndian(slice) : BinaryPrimitives.ReadUInt32BigEndian(slice);
    }

    private static double ReadDouble(ReadOnlySpan<byte> buf, ref int pos, bool le)
    {
        var slice = buf.Slice(pos, 8);
        pos += 8;
        return le ? BinaryPrimitives.ReadDoubleLittleEndian(slice) : BinaryPrimitives.ReadDoubleBigEndian(slice);
    }
}
