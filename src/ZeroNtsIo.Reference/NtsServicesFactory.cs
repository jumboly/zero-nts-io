using NetTopologySuite;
using NetTopologySuite.Geometries.Implementation;

namespace ZeroNtsIo.Reference;

public static class NtsServicesFactory
{
    // Why: 既定の CoordinateArraySequenceFactory は明示的な dimension を指定しないと
    // ラウンドトリップ時に Z/M を黙って落とす。PackedCoordinateSequenceFactory なら
    // WKB/WKT Reader が渡すシーケンス毎の dimension / measures ヒントを尊重してくれる。
    public static NtsGeometryServices CreatePacked() =>
        new NtsGeometryServices(PackedCoordinateSequenceFactory.DoubleFactory);
}
