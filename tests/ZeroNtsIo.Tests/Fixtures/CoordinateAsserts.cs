using NetTopologySuite.Geometries;
using Xunit;

namespace ZeroNtsIo.Tests.Fixtures;

public static class CoordinateAsserts
{
    // Why: 浮動小数点のテキストラウンドトリップは、パーサによって結果が 1 ULP ずれ得る。
    // ulpTolerance == 0 の場合はビット単位で完全一致を要求し、それ以外は小さな差を許容する。
    public static void AssertCoordinatesBitEqual(Geometry expected, Geometry actual, long ulpTolerance = 0)
    {
        Assert.Equal(expected.GeometryType, actual.GeometryType);
        Assert.Equal(expected.NumGeometries, actual.NumGeometries);
        Assert.Equal(expected.IsEmpty, actual.IsEmpty);

        var e = Flatten(expected);
        var a = Flatten(actual);
        Assert.Equal(e.Count, a.Count);
        for (int i = 0; i < e.Count; i++)
        {
            long eb = BitConverter.DoubleToInt64Bits(e[i]);
            long ab = BitConverter.DoubleToInt64Bits(a[i]);
            long diff = Math.Abs(eb - ab);
            // この比較では、両辺が NaN であれば等しいものとして扱う。
            if (double.IsNaN(e[i]) && double.IsNaN(a[i])) continue;
            Assert.True(diff <= ulpTolerance,
                $"[{i}] expected={e[i]:R} (bits {eb:X}) actual={a[i]:R} (bits {ab:X}) ulp diff={diff}");
        }
    }

    public static List<double> Flatten(Geometry g)
    {
        var list = new List<double>();
        FlattenInto(g, list);
        return list;
    }

    private static void FlattenInto(Geometry g, List<double> into)
    {
        switch (g)
        {
            case Point p:
                if (p.IsEmpty) { into.Add(double.NaN); into.Add(double.NaN); break; }
                AddSeq(p.CoordinateSequence, into);
                break;
            case LineString ls:
                AddSeq(ls.CoordinateSequence, into);
                break;
            case Polygon poly:
                if (poly.IsEmpty) break;
                AddSeq(poly.ExteriorRing.CoordinateSequence, into);
                for (int i = 0; i < poly.NumInteriorRings; i++)
                    AddSeq(poly.GetInteriorRingN(i).CoordinateSequence, into);
                break;
            default:
                for (int i = 0; i < g.NumGeometries; i++)
                    FlattenInto(g.GetGeometryN(i), into);
                break;
        }
    }

    private static void AddSeq(CoordinateSequence seq, List<double> into)
    {
        for (int i = 0; i < seq.Count; i++)
        {
            into.Add(seq.GetX(i));
            into.Add(seq.GetY(i));
            if (seq.HasZ) into.Add(seq.GetZ(i));
            if (seq.HasM) into.Add(seq.GetM(i));
        }
    }
}
