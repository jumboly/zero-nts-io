using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using ZeroWkX.Internal;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NetTopologySuite.IO;

namespace NetTopologySuite.IO;

/// <summary>
/// Zero-copy WKB writer. For <see cref="PackedDoubleCoordinateSequence"/> inputs, the underlying
/// <c>double[]</c> is reinterpreted as bytes and copied in one shot (LE) or SIMD byte-swapped (BE).
/// </summary>
public sealed class ZWkbWriter
{
    public byte[] Write(Geometry geometry, ByteOrder byteOrder = ByteOrder.LittleEndian)
    {
        var bw = new ArrayBufferWriter<byte>(EstimateSize(geometry));
        bool le = byteOrder == ByteOrder.LittleEndian;
        WriteGeometry(bw, geometry, le);
        return bw.WrittenSpan.ToArray();
    }

    private static void WriteGeometry(ArrayBufferWriter<byte> bw, Geometry g, bool le)
    {
        var (baseType, seq) = Classify(g);
        var (ordCode, dim) = OrdinateOf(seq);
        // Why: NTS suppresses the Z/M offset on the MultiPoint header only (historical SFS
        // convention) — MultiLineString, MultiPolygon, and GeometryCollection still carry the
        // dimension offset. Matching NTS exactly is required for byte-level interop.
        bool suppressOrdOffset = g is MultiPoint;
        uint typeCode = (uint)baseType + (suppressOrdOffset ? 0u : ordCode * 1000u);
        WriteByte(bw, (byte)(le ? 1 : 0));
        WriteUInt32(bw, typeCode, le);

        switch (g)
        {
            case Point p:
                WritePointBody(bw, p, le, dim);
                break;
            case LineString ls:
                WriteSequence(bw, ls.CoordinateSequence, le, dim);
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

    private static void WritePointBody(ArrayBufferWriter<byte> bw, Point p, bool le, int dim)
    {
        if (p.IsEmpty)
        {
            Span<byte> nan = stackalloc byte[8];
            BinaryPrimitives.WriteDoubleLittleEndian(nan, double.NaN);
            for (int i = 0; i < dim; i++) bw.Write(nan);
            return;
        }
        WriteSequenceCoord(bw, p.CoordinateSequence, 0, le, dim);
    }

    private static void WritePolygon(ArrayBufferWriter<byte> bw, Polygon poly, bool le, int dim)
    {
        int rings = poly.IsEmpty ? 0 : 1 + poly.NumInteriorRings;
        WriteUInt32(bw, (uint)rings, le);
        if (rings == 0) return;
        WriteSequence(bw, poly.ExteriorRing.CoordinateSequence, le, dim);
        for (int r = 0; r < poly.NumInteriorRings; r++)
            WriteSequence(bw, poly.GetInteriorRingN(r).CoordinateSequence, le, dim);
    }

    private static void WriteSequence(ArrayBufferWriter<byte> bw, CoordinateSequence seq, bool le, int dim)
    {
        int n = seq.Count;
        WriteUInt32(bw, (uint)n, le);
        if (n == 0) return;

        // Why: PackedDoubleCoordinateSequence holds ordinates as a contiguous double[] matching
        // the OGC WKB-LE byte layout, so on LE targets we can emit the entire coord block in one
        // byte copy; on BE we SIMD byte-swap through CoordinateBlockReader.
        if (seq is PackedDoubleCoordinateSequence pdc && pdc.Dimension == dim)
        {
            var raw = pdc.GetRawCoordinates();
            var dst = bw.GetSpan(raw.Length * sizeof(double));
            if (le) CoordinateBlockReader.WriteLittleEndian(raw, dst);
            else CoordinateBlockReader.WriteBigEndian(raw, dst);
            bw.Advance(raw.Length * sizeof(double));
            return;
        }

        for (int i = 0; i < n; i++) WriteSequenceCoord(bw, seq, i, le, dim);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteSequenceCoord(ArrayBufferWriter<byte> bw, CoordinateSequence seq, int i, bool le, int dim)
    {
        Span<byte> buf = stackalloc byte[8];
        WriteD(buf, seq.GetX(i), le); bw.Write(buf);
        WriteD(buf, seq.GetY(i), le); bw.Write(buf);
        if (seq.HasZ) { WriteD(buf, seq.GetZ(i), le); bw.Write(buf); }
        if (seq.HasM) { WriteD(buf, seq.GetM(i), le); bw.Write(buf); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteD(Span<byte> buf, double v, bool le)
    {
        if (le) BinaryPrimitives.WriteDoubleLittleEndian(buf, v);
        else BinaryPrimitives.WriteDoubleBigEndian(buf, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteByte(ArrayBufferWriter<byte> bw, byte b)
    {
        var dst = bw.GetSpan(1);
        dst[0] = b;
        bw.Advance(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteUInt32(ArrayBufferWriter<byte> bw, uint v, bool le)
    {
        var dst = bw.GetSpan(4);
        if (le) BinaryPrimitives.WriteUInt32LittleEndian(dst, v);
        else BinaryPrimitives.WriteUInt32BigEndian(dst, v);
        bw.Advance(4);
    }

    private static (int baseType, CoordinateSequence? seq) Classify(Geometry g) => g switch
    {
        Point p => (1, p.IsEmpty ? null : p.CoordinateSequence),
        LineString ls => (2, ls.IsEmpty ? null : ls.CoordinateSequence),
        Polygon poly => (3, poly.IsEmpty ? null : poly.ExteriorRing.CoordinateSequence),
        MultiPoint mp => (4, FirstSeq(mp)),
        MultiLineString mls => (5, FirstSeq(mls)),
        MultiPolygon mpo => (6, FirstSeq(mpo)),
        GeometryCollection gc => (7, FirstSeq(gc)),
        _ => throw new NotSupportedException(g.GeometryType),
    };

    private static (uint code, int dim) OrdinateOf(CoordinateSequence? seq)
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

    private static int EstimateSize(Geometry g)
    {
        // Why: rough upper bound — 1 + 4 header + 4 count per sub-part + 8 bytes per ordinate.
        int coords = g.NumPoints;
        return 64 + coords * 32;
    }
}
