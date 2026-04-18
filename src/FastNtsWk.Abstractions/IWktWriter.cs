using NetTopologySuite.Geometries;

namespace FastNtsWk.Abstractions;

public interface IWktWriter
{
    string Write(Geometry geometry);
}
