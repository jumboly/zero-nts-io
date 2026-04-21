using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace ZeroNtsIo.Reference;

public sealed class NtsWkbWriter
{
    public byte[] Write(Geometry geometry, ByteOrder byteOrder = ByteOrder.LittleEndian)
    {
        // Why: Z / M の出力有無は実シーケンスのフラグと独立に反映する必要がある
        // （例: POINT M は HasM=true かつ HasZ=false なので、emitZ=true を渡すと XYZM として書かれて M が壊れる）。
        var writer = new WKBWriter(byteOrder, handleSRID: false, emitZ: HasZ(geometry), emitM: HasM(geometry));
        return writer.Write(geometry);
    }

    private static bool HasZ(Geometry g) => AnySeq(g, s => s.HasZ);
    private static bool HasM(Geometry g) => AnySeq(g, s => s.HasM);

    private static bool AnySeq(Geometry g, Func<CoordinateSequence, bool> pred)
    {
        // Why: Multi* / GeometryCollection は NumGeometries >= 1、Point / LineString / Polygon は 1 を返す。
        // コンテナ型（SequenceOf が null を返す型）では子へ再帰し、リーフ型では自身のシーケンスを見る必要がある。
        // NumGeometries > 1 で「コンテナか」を判定すると、単一子コンテナで誤判定するため、型で判定する。
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
