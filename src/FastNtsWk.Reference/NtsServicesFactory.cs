using NetTopologySuite;
using NetTopologySuite.Geometries.Implementation;

namespace FastNtsWk.Reference;

public static class NtsServicesFactory
{
    // Why: the default CoordinateArraySequenceFactory drops Z/M silently on round-trip
    // unless constructed with explicit dimension. PackedCoordinateSequenceFactory honors
    // the per-sequence dimension/measures hints that WKB/WKT readers pass in.
    public static NtsGeometryServices CreatePacked() =>
        new NtsGeometryServices(PackedCoordinateSequenceFactory.DoubleFactory);
}
