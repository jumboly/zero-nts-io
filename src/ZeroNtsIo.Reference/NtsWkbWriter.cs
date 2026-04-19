using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace ZeroNtsIo.Reference;

public sealed class NtsWkbWriter
{
    public byte[] Write(Geometry geometry, ByteOrder byteOrder = ByteOrder.LittleEndian)
    {
        // Why: Z and M emission must track the actual sequence flags independently (a POINT M
        // has HasM=true but HasZ=false; passing emitZ=true would emit XYZM bytes and corrupt M).
        var writer = new WKBWriter(byteOrder, handleSRID: false, emitZ: HasZ(geometry), emitM: HasM(geometry));
        return writer.Write(geometry);
    }

    private static bool HasZ(Geometry g) => AnySeq(g, s => s.HasZ);
    private static bool HasM(Geometry g) => AnySeq(g, s => s.HasM);

    private static bool AnySeq(Geometry g, Func<CoordinateSequence, bool> pred)
    {
        // Why: Multi*/GeometryCollection expose NumGeometries >= 1; Point/LineString/Polygon
        // expose 1. We must recurse into container children (where SequenceOf returns null)
        // but inspect the own sequence for leaf types. Detecting "is container" by type is
        // more correct than using NumGeometries > 1 (fails for single-child containers).
        if (g is MultiPoint or MultiLineString or MultiPolygon or GeometryCollection)
        {
            for (int i = 0; i < g.NumGeometries; i++)
                if (AnySeq(g.GetGeometryN(i), pred)) return true;
            return false;
        }
        var seq = SequenceOf(g);
        return seq is not null && pred(seq);
    }

    private static CoordinateSequence? SequenceOf(Geometry g) => g switch
    {
        Point p => p.IsEmpty ? null : p.CoordinateSequence,
        LineString ls => ls.IsEmpty ? null : ls.CoordinateSequence,
        Polygon poly => poly.IsEmpty ? null : poly.ExteriorRing.CoordinateSequence,
        _ => null,
    };
}
