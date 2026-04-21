using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;

namespace ZeroNtsIo.Internal;

/// <summary>
/// 新規確保した <c>double[]</c> を <see cref="PackedCoordinateSequenceFactory.Create(double[], int, int)"/>
/// 経由で NTS に渡すためのヘルパー。<c>PackedType.Double</c> 設定時はコピーなしで所有権が受け渡される。
/// </summary>
internal static class PackedSequenceBuilder
{
    public static int DimensionOf(Ordinates ord) => ord switch
    {
        Ordinates.XY => 2,
        Ordinates.XYZ => 3,
        Ordinates.XYM => 3,
        Ordinates.XYZM => 4,
        _ => 2,
    };

    public static int MeasuresOf(Ordinates ord) => ord switch
    {
        Ordinates.XYM => 1,
        Ordinates.XYZM => 1,
        _ => 0,
    };

    public static CoordinateSequence Create(CoordinateSequenceFactory factory, double[] packed, int dimension, int measures)
    {
        if (factory is PackedCoordinateSequenceFactory pf && pf.Type == PackedCoordinateSequenceFactory.PackedType.Double)
        {
            // Why: PackedCoordinateSequenceFactory.Create(double[], dim, measures) は配列の所有権を
            // コピーなしで受け取る。WKB-LE バイトと、WKT 用に直接埋めた座標バッファを、
            // ここをゼロコピーで流し込む受け口として使う。
            return pf.Create(packed, dimension, measures);
        }
        throw new InvalidOperationException(
            "ZeroNtsIo requires PackedCoordinateSequenceFactory.DoubleFactory — see NtsServicesFactory.CreatePacked() in ZeroNtsIo.Reference.");
    }
}
