using System.Buffers.Binary;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace ZeroNtsIo.Naive;

public sealed class NaiveWkbWriter
{
    public byte[] Write(Geometry geometry, ByteOrder byteOrder = ByteOrder.LittleEndian)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bool le = byteOrder == ByteOrder.LittleEndian;
        WriteGeometry(bw, geometry, le);
        return ms.ToArray();
    }

    private static void WriteGeometry(BinaryWriter bw, Geometry g, bool le)
    {
        var (baseType, seq) = Classify(g);
        var (ordCode, dim) = OrdOf(seq);

        bw.Write((byte)(le ? 1 : 0));
        uint type = (uint)baseType + ordCode * 1000u;
        WriteUInt32(bw, type, le);

        switch (g)
        {
            case Point p:
                WritePointBody(bw, p, le, dim);
                break;
            case LineString ls:
                WriteLineString(bw, ls.CoordinateSequence, le, dim);
                break;
            case Polygon poly:
                WritePolygon(bw, poly, le, dim);
                break;
            case MultiPoint mp:
                WriteUInt32(bw, (uint)mp.NumGeometries, le);
                for (int i = 0; i < mp.NumGeometries; i++) WriteGeometry(bw, mp.GetGeometryN(i), le);
                break;
            case MultiLineString mls:
                WriteUInt32(bw, (uint)mls.NumGeometries, le);
                for (int i = 0; i < mls.NumGeometries; i++) WriteGeometry(bw, mls.GetGeometryN(i), le);
                break;
            case MultiPolygon mpo:
                WriteUInt32(bw, (uint)mpo.NumGeometries, le);
                for (int i = 0; i < mpo.NumGeometries; i++) WriteGeometry(bw, mpo.GetGeometryN(i), le);
                break;
            case GeometryCollection gc:
                WriteUInt32(bw, (uint)gc.NumGeometries, le);
                for (int i = 0; i < gc.NumGeometries; i++) WriteGeometry(bw, gc.GetGeometryN(i), le);
                break;
            default:
                throw new NotSupportedException(g.GeometryType);
        }
    }

    private static void WritePointBody(BinaryWriter bw, Point p, bool le, int dim)
    {
        if (p.IsEmpty)
        {
            // Why: OGC represents POINT EMPTY as all-NaN coordinates.
            for (int i = 0; i < dim; i++) WriteDouble(bw, double.NaN, le);
            return;
        }
        WriteCoord(bw, p.CoordinateSequence, 0, le);
    }

    private static void WriteLineString(BinaryWriter bw, CoordinateSequence seq, bool le, int dim)
    {
        WriteUInt32(bw, (uint)seq.Count, le);
        for (int i = 0; i < seq.Count; i++) WriteCoord(bw, seq, i, le);
    }

    private static void WritePolygon(BinaryWriter bw, Polygon poly, bool le, int dim)
    {
        int rings = poly.IsEmpty ? 0 : 1 + poly.NumInteriorRings;
        WriteUInt32(bw, (uint)rings, le);
        if (rings == 0) return;
        WriteLineString(bw, poly.ExteriorRing.CoordinateSequence, le, dim);
        for (int r = 0; r < poly.NumInteriorRings; r++)
            WriteLineString(bw, poly.GetInteriorRingN(r).CoordinateSequence, le, dim);
    }

    private static void WriteCoord(BinaryWriter bw, CoordinateSequence seq, int i, bool le)
    {
        WriteDouble(bw, seq.GetX(i), le);
        WriteDouble(bw, seq.GetY(i), le);
        if (seq.HasZ) WriteDouble(bw, seq.GetZ(i), le);
        if (seq.HasM) WriteDouble(bw, seq.GetM(i), le);
    }

    private static (int baseType, CoordinateSequence? seq) Classify(Geometry g) => g switch
    {
        Point p => (1, p.CoordinateSequence),
        LineString ls => (2, ls.CoordinateSequence),
        Polygon poly => (3, poly.IsEmpty ? null : poly.ExteriorRing.CoordinateSequence),
        MultiPoint mp => (4, FirstSeq(mp)),
        MultiLineString mls => (5, FirstSeq(mls)),
        MultiPolygon mpo => (6, FirstSeq(mpo)),
        GeometryCollection gc => (7, FirstSeq(gc)),
        _ => throw new NotSupportedException(g.GeometryType),
    };

    private static (uint code, int dim) OrdOf(CoordinateSequence? seq)
    {
        if (seq is null) return (0, 2);
        bool z = seq.HasZ, m = seq.HasM;
        if (z && m) return (3, 4);
        if (z) return (1, 3);
        if (m) return (2, 3);
        return (0, 2);
    }

    private static CoordinateSequence? FirstSeq(Geometry g)
    {
        if (g.IsEmpty) return null;
        return g.GetGeometryN(0) switch
        {
            Point p => p.CoordinateSequence,
            LineString ls => ls.CoordinateSequence,
            Polygon poly => poly.IsEmpty ? null : poly.ExteriorRing.CoordinateSequence,
            Geometry inner => FirstSeq(inner),
        };
    }

    private static void WriteUInt32(BinaryWriter bw, uint v, bool le)
    {
        Span<byte> buf = stackalloc byte[4];
        if (le) BinaryPrimitives.WriteUInt32LittleEndian(buf, v);
        else BinaryPrimitives.WriteUInt32BigEndian(buf, v);
        bw.Write(buf);
    }

    private static void WriteDouble(BinaryWriter bw, double v, bool le)
    {
        Span<byte> buf = stackalloc byte[8];
        if (le) BinaryPrimitives.WriteDoubleLittleEndian(buf, v);
        else BinaryPrimitives.WriteDoubleBigEndian(buf, v);
        bw.Write(buf);
    }
}
