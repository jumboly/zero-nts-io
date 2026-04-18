using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;

namespace NetTopologySuite.IO.ZeroWkX.Internal;

/// <summary>
/// Helpers for handing a freshly allocated <c>double[]</c> to NTS via
/// <see cref="PackedCoordinateSequenceFactory.Create(double[], int, int)"/>, which takes ownership
/// without copying when <c>PackedType.Double</c> is configured.
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
            // Why: PackedCoordinateSequenceFactory.Create(double[], dim, measures) takes ownership
            // of the array without copying. This is the zero-copy sink for WKB-LE bytes and for
            // our directly-populated WKT coord buffer.
            return pf.Create(packed, dimension, measures);
        }
        throw new InvalidOperationException(
            "NetTopologySuite.IO.ZeroWkX requires PackedCoordinateSequenceFactory.DoubleFactory — see NtsServicesFactory.CreatePacked() in ZeroWkX.Reference.");
    }
}
