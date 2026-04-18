using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.IO;

/// <summary>
/// WKT writer that formats each ordinate via <c>double.TryFormat</c> into a stack-allocated span
/// and appends it directly, bypassing <c>string</c> allocations per coordinate.
/// </summary>
public sealed class ZWktWriter
{
    public string Write(Geometry geometry)
    {
        // Why: rough upper bound — ~30 chars per ordinate and up to 4 ordinates per coord.
        var sb = new StringBuilder(capacity: Math.Max(32, geometry.NumPoints * 120));
        WriteGeometry(sb, geometry);
        return sb.ToString();
    }

    private static void WriteGeometry(StringBuilder sb, Geometry g)
    {
        switch (g)
        {
            case Point p: WritePoint(sb, p); break;
            case LineString ls: WriteLineString(sb, ls); break;
            case Polygon poly: WritePolygon(sb, poly); break;
            case MultiPoint mp: WriteMultiPoint(sb, mp); break;
            case MultiLineString mls: WriteMultiLineString(sb, mls); break;
            case MultiPolygon mpo: WriteMultiPolygon(sb, mpo); break;
            case GeometryCollection gc: WriteGeometryCollection(sb, gc); break;
            default: throw new NotSupportedException(g.GeometryType);
        }
    }

    private static void WritePoint(StringBuilder sb, Point p)
    {
        sb.Append("POINT");
        AppendModifier(sb, p.CoordinateSequence);
        if (p.IsEmpty) { sb.Append(" EMPTY"); return; }
        sb.Append(" (");
        AppendCoord(sb, p.CoordinateSequence, 0);
        sb.Append(')');
    }

    private static void WriteLineString(StringBuilder sb, LineString ls)
    {
        sb.Append("LINESTRING");
        AppendModifier(sb, ls.CoordinateSequence);
        if (ls.IsEmpty) { sb.Append(" EMPTY"); return; }
        sb.Append(" (");
        AppendSequence(sb, ls.CoordinateSequence);
        sb.Append(')');
    }

    private static void WritePolygon(StringBuilder sb, Polygon poly)
    {
        sb.Append("POLYGON");
        AppendModifier(sb, poly.IsEmpty ? null : poly.ExteriorRing.CoordinateSequence);
        if (poly.IsEmpty) { sb.Append(" EMPTY"); return; }
        sb.Append(" ((");
        AppendSequence(sb, poly.ExteriorRing.CoordinateSequence);
        sb.Append(')');
        for (int i = 0; i < poly.NumInteriorRings; i++)
        {
            sb.Append(", (");
            AppendSequence(sb, poly.GetInteriorRingN(i).CoordinateSequence);
            sb.Append(')');
        }
        sb.Append(')');
    }

    private static void WriteMultiPoint(StringBuilder sb, MultiPoint mp)
    {
        sb.Append("MULTIPOINT");
        AppendModifier(sb, FirstSeq(mp));
        if (mp.IsEmpty) { sb.Append(" EMPTY"); return; }
        sb.Append(" (");
        for (int i = 0; i < mp.NumGeometries; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('(');
            AppendCoord(sb, ((Point)mp.GetGeometryN(i)).CoordinateSequence, 0);
            sb.Append(')');
        }
        sb.Append(')');
    }

    private static void WriteMultiLineString(StringBuilder sb, MultiLineString mls)
    {
        sb.Append("MULTILINESTRING");
        AppendModifier(sb, FirstSeq(mls));
        if (mls.IsEmpty) { sb.Append(" EMPTY"); return; }
        sb.Append(" (");
        for (int i = 0; i < mls.NumGeometries; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('(');
            AppendSequence(sb, ((LineString)mls.GetGeometryN(i)).CoordinateSequence);
            sb.Append(')');
        }
        sb.Append(')');
    }

    private static void WriteMultiPolygon(StringBuilder sb, MultiPolygon mpo)
    {
        sb.Append("MULTIPOLYGON");
        AppendModifier(sb, FirstSeq(mpo));
        if (mpo.IsEmpty) { sb.Append(" EMPTY"); return; }
        sb.Append(" (");
        for (int i = 0; i < mpo.NumGeometries; i++)
        {
            if (i > 0) sb.Append(", ");
            var poly = (Polygon)mpo.GetGeometryN(i);
            sb.Append("((");
            AppendSequence(sb, poly.ExteriorRing.CoordinateSequence);
            sb.Append(')');
            for (int r = 0; r < poly.NumInteriorRings; r++)
            {
                sb.Append(", (");
                AppendSequence(sb, poly.GetInteriorRingN(r).CoordinateSequence);
                sb.Append(')');
            }
            sb.Append(')');
        }
        sb.Append(')');
    }

    private static void WriteGeometryCollection(StringBuilder sb, GeometryCollection gc)
    {
        sb.Append("GEOMETRYCOLLECTION");
        if (gc.IsEmpty) { sb.Append(" EMPTY"); return; }
        sb.Append(" (");
        for (int i = 0; i < gc.NumGeometries; i++)
        {
            if (i > 0) sb.Append(", ");
            WriteGeometry(sb, gc.GetGeometryN(i));
        }
        sb.Append(')');
    }

    private static void AppendModifier(StringBuilder sb, CoordinateSequence? seq)
    {
        if (seq is null) return;
        bool z = seq.HasZ, m = seq.HasM;
        if (z && m) sb.Append(" ZM");
        else if (z) sb.Append(" Z");
        else if (m) sb.Append(" M");
    }

    private static void AppendSequence(StringBuilder sb, CoordinateSequence seq)
    {
        for (int i = 0; i < seq.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            AppendCoord(sb, seq, i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendCoord(StringBuilder sb, CoordinateSequence seq, int i)
    {
        Span<char> buf = stackalloc char[32];
        AppendDouble(sb, buf, seq.GetX(i));
        sb.Append(' ');
        AppendDouble(sb, buf, seq.GetY(i));
        if (seq.HasZ) { sb.Append(' '); AppendDouble(sb, buf, seq.GetZ(i)); }
        if (seq.HasM) { sb.Append(' '); AppendDouble(sb, buf, seq.GetM(i)); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendDouble(StringBuilder sb, Span<char> buf, double v)
    {
        if (!v.TryFormat(buf, out int written, "R", CultureInfo.InvariantCulture))
        {
            sb.Append(v.ToString("R", CultureInfo.InvariantCulture));
            return;
        }
        sb.Append(buf.Slice(0, written));
    }

    private static CoordinateSequence? FirstSeq(Geometry g)
    {
        if (g.IsEmpty) return null;
        return g.GetGeometryN(0) switch
        {
            Point p => p.CoordinateSequence,
            LineString ls => ls.CoordinateSequence,
            Polygon poly => poly.IsEmpty ? null : poly.ExteriorRing.CoordinateSequence,
            _ => null,
        };
    }
}
