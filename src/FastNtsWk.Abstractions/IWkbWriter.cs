using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace FastNtsWk.Abstractions;

public interface IWkbWriter
{
    byte[] Write(Geometry geometry, ByteOrder byteOrder = ByteOrder.LittleEndian);
}
