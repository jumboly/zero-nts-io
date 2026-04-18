using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using ZeroWkX.Internal;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.IO;

/// <summary>
/// Fast WKB reader: unsafe / SIMD coordinate block copy. LE blocks are reinterpreted via
/// <see cref="CoordinateBlockReader.ReadLittleEndian"/> (one <c>memcpy</c>); BE blocks are
/// byte-swapped via a <c>Vector128.Shuffle</c> loop. Coordinates are handed to
/// <see cref="NetTopologySuite.Geometries.Implementation.PackedCoordinateSequenceFactory"/>
/// with ownership transfer, so the WKB payload is never materialized as
/// <see cref="Coordinate"/> objects.
/// </summary>
public sealed class ZWkbReader
{
    private readonly GeometryFactory _factory;
    private readonly CoordinateSequenceFactory _seqFactory;

    public ZWkbReader(NtsGeometryServices? services = null)
    {
        _factory = (services ?? NtsGeometryServices.Instance).CreateGeometryFactory();
        _seqFactory = _factory.CoordinateSequenceFactory;
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
        int dim, measures;
        switch (ordCode)
        {
            case 0: dim = 2; measures = 0; break;
            case 1: dim = 3; measures = 0; break;
            case 2: dim = 3; measures = 1; break;
            case 3: dim = 4; measures = 1; break;
            default: throw new FormatException($"Unknown ordinate code: {ordCode}");
        }

        return baseType switch
        {
            1 => ReadPoint(buf, ref pos, le, dim, measures),
            2 => ReadLineString(buf, ref pos, le, dim, measures),
            3 => ReadPolygon(buf, ref pos, le, dim, measures),
            4 => ReadMulti(buf, ref pos, le, kind: 4),
            5 => ReadMulti(buf, ref pos, le, kind: 5),
            6 => ReadMulti(buf, ref pos, le, kind: 6),
            7 => ReadCollection(buf, ref pos, le),
            _ => throw new FormatException($"Unknown WKB geometry type: {baseType}"),
        };
    }

    private Point ReadPoint(ReadOnlySpan<byte> buf, ref int pos, bool le, int dim, int measures)
    {
        var packed = new double[dim];
        CopyCoords(buf.Slice(pos, dim * 8), packed, le);
        pos += dim * 8;

        // Why: OGC POINT EMPTY is encoded as all-NaN ordinates.
        if (double.IsNaN(packed[0]) && double.IsNaN(packed[1]))
            return _factory.CreatePoint((Coordinate?)null);

        var seq = PackedSequenceBuilder.Create(_seqFactory, packed, dim, measures);
        return _factory.CreatePoint(seq);
    }

    private LineString ReadLineString(ReadOnlySpan<byte> buf, ref int pos, bool le, int dim, int measures)
    {
        int n = (int)ReadUInt32(buf, ref pos, le);
        var seq = ReadSequence(buf, ref pos, le, n, dim, measures);
        return _factory.CreateLineString(seq);
    }

    private Polygon ReadPolygon(ReadOnlySpan<byte> buf, ref int pos, bool le, int dim, int measures)
    {
        int nRings = (int)ReadUInt32(buf, ref pos, le);
        if (nRings == 0) return _factory.CreatePolygon((LinearRing?)null);
        var rings = new LinearRing[nRings];
        for (int r = 0; r < nRings; r++)
        {
            int n = (int)ReadUInt32(buf, ref pos, le);
            var seq = ReadSequence(buf, ref pos, le, n, dim, measures);
            rings[r] = _factory.CreateLinearRing(seq);
        }
        var holes = nRings > 1 ? rings.AsSpan(1).ToArray() : Array.Empty<LinearRing>();
        return _factory.CreatePolygon(rings[0], holes);
    }

    private Geometry ReadMulti(ReadOnlySpan<byte> buf, ref int pos, bool le, int kind)
    {
        int n = (int)ReadUInt32(buf, ref pos, le);
        switch (kind)
        {
            case 4:
            {
                var arr = new Point[n];
                for (int i = 0; i < n; i++) arr[i] = (Point)ReadGeometry(buf, ref pos);
                return _factory.CreateMultiPoint(arr);
            }
            case 5:
            {
                var arr = new LineString[n];
                for (int i = 0; i < n; i++) arr[i] = (LineString)ReadGeometry(buf, ref pos);
                return _factory.CreateMultiLineString(arr);
            }
            case 6:
            {
                var arr = new Polygon[n];
                for (int i = 0; i < n; i++) arr[i] = (Polygon)ReadGeometry(buf, ref pos);
                return _factory.CreateMultiPolygon(arr);
            }
            default: throw new InvalidOperationException();
        }
    }

    private GeometryCollection ReadCollection(ReadOnlySpan<byte> buf, ref int pos, bool le)
    {
        int n = (int)ReadUInt32(buf, ref pos, le);
        var arr = new Geometry[n];
        for (int i = 0; i < n; i++) arr[i] = ReadGeometry(buf, ref pos);
        return _factory.CreateGeometryCollection(arr);
    }

    private CoordinateSequence ReadSequence(ReadOnlySpan<byte> buf, ref int pos, bool le, int count, int dim, int measures)
    {
        int bytes = count * dim * 8;
        var packed = new double[count * dim];
        CopyCoords(buf.Slice(pos, bytes), packed, le);
        pos += bytes;
        return PackedSequenceBuilder.Create(_seqFactory, packed, dim, measures);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyCoords(ReadOnlySpan<byte> src, double[] dst, bool le)
    {
        if (le) CoordinateBlockReader.ReadLittleEndian(src, dst);
        else CoordinateBlockReader.ReadBigEndian(src, dst);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadUInt32(ReadOnlySpan<byte> buf, ref int pos, bool le)
    {
        var slice = buf.Slice(pos, 4);
        pos += 4;
        return le ? BinaryPrimitives.ReadUInt32LittleEndian(slice) : BinaryPrimitives.ReadUInt32BigEndian(slice);
    }
}
