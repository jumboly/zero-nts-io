using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using ZeroNtsIo.Internal;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace ZeroNtsIo;

/// <summary>
/// 高速 WKB Reader。unsafe / SIMD による座標ブロックコピーを行う。
/// LE ブロックは <see cref="CoordinateBlockReader.ReadLittleEndian"/> で再解釈（<c>memcpy</c> 1 回）し、
/// BE ブロックは <c>Vector128.Shuffle</c> ループでバイトスワップする。
/// 座標は所有権を渡す形で
/// <see cref="NetTopologySuite.Geometries.Implementation.PackedCoordinateSequenceFactory"/> に受け渡すため、
/// WKB ペイロードは <see cref="Coordinate"/> オブジェクトとして一切マテリアライズされない。
/// </summary>
public sealed class ZWkbReader
{
    private readonly GeometryFactory _factory;
    private readonly CoordinateSequenceFactory _seqFactory;

    public ZWkbReader(NtsGeometryServices? services = null)
    {
        _factory = (services ?? NtsGeometryServices.Instance).CreateGeometryFactory();
        _seqFactory = _factory.CoordinateSequenceFactory;
    }

    public Geometry Read(byte[] wkb) => Read(wkb.AsSpan());

    public Geometry Read(ReadOnlySpan<byte> wkb)
    {
        int pos = 0;
        int capturedSrid = 0;
        var g = ReadGeometry(wkb, ref pos, ref capturedSrid);
        // Why: PostGIS EWKB では SRID はルートのタイプコードにのみ付く。戻り値のジオメトリへ反映する。
        // NTS の SRID setter は collection 系では子へ再帰的に伝播する。
        if (capturedSrid != 0) g.SRID = capturedSrid;
        return g;
    }

    private Geometry ReadGeometry(ReadOnlySpan<byte> buf, ref int pos, ref int capturedSrid)
    {
        byte bo = buf[pos++];
        if (bo != 0 && bo != 1) throw new FormatException($"Invalid byte order: {bo}");
        bool le = bo == 1;

        uint rawType = ReadUInt32(buf, ref pos, le);
        uint ogc = rawType;
        bool hiZ = false, hiM = false;
        // Why: EWKB は SRID / Z / M を高位ビットフラグでエンコードし、OGC の 1000/2000/3000 オフセットとは直交する。
        // 共通の OGC-ISO 経路を分岐なしに保つため、EWKB デコード全体を 1 つのマスク判定の内側に閉じ込める。
        // OGC ジオメトリの子側はこのブロックに再入しない。
        if ((rawType & EwkbFlags.Any) != 0)
        {
            if ((rawType & EwkbFlags.Srid) != 0)
            {
                int srid = (int)ReadUInt32(buf, ref pos, le);
                // Why: 最外郭ジオメトリの SRID のみが意味を持つ。PostGIS は子では省略するが、
                // 生成側によっては子にも付けるため、最初に現れた値（= ルートの値）を採用する。
                if (capturedSrid == 0) capturedSrid = srid;
            }
            hiZ = (rawType & EwkbFlags.Z) != 0;
            hiM = (rawType & EwkbFlags.M) != 0;
            ogc = rawType & EwkbFlags.OgcMask;
        }

        uint ordCode = ogc / 1000u;
        if (ordCode > 3) throw new FormatException($"Unknown ordinate code: {ordCode}");
        uint baseType = ogc % 1000u;

        bool isZ = hiZ || ordCode == 1 || ordCode == 3;
        bool isM = hiM || ordCode == 2 || ordCode == 3;
        int dim = 2 + (isZ ? 1 : 0) + (isM ? 1 : 0);
        int measures = isM ? 1 : 0;

        return baseType switch
        {
            1 => ReadPoint(buf, ref pos, le, dim, measures),
            2 => ReadLineString(buf, ref pos, le, dim, measures),
            3 => ReadPolygon(buf, ref pos, le, dim, measures),
            4 => ReadMulti(buf, ref pos, le, kind: 4, ref capturedSrid),
            5 => ReadMulti(buf, ref pos, le, kind: 5, ref capturedSrid),
            6 => ReadMulti(buf, ref pos, le, kind: 6, ref capturedSrid),
            7 => ReadCollection(buf, ref pos, le, ref capturedSrid),
            _ => throw new FormatException($"Unknown WKB geometry type: {baseType}"),
        };
    }

    private Point ReadPoint(ReadOnlySpan<byte> buf, ref int pos, bool le, int dim, int measures)
    {
        var packed = new double[dim];
        CopyCoords(buf.Slice(pos, dim * 8), packed, le);
        pos += dim * 8;

        // Why: OGC 仕様では POINT EMPTY は全 ordinates が NaN としてエンコードされる。
        if (double.IsNaN(packed[0]) && double.IsNaN(packed[1]))
            return _factory.CreatePoint((Coordinate?)null);

        var seq = PackedSequenceBuilder.Create(_seqFactory, packed, dim, measures);
        return _factory.CreatePoint(seq);
    }

    private LineString ReadLineString(ReadOnlySpan<byte> buf, ref int pos, bool le, int dim, int measures)
    {
        int n = (int)ReadUInt32(buf, ref pos, le);
        var seq = ReadSequence(buf, ref pos, le, n, dim, measures);
        return _factory.CreateLineString(seq);
    }

    private Polygon ReadPolygon(ReadOnlySpan<byte> buf, ref int pos, bool le, int dim, int measures)
    {
        int nRings = (int)ReadUInt32(buf, ref pos, le);
        if (nRings == 0) return _factory.CreatePolygon((LinearRing?)null);
        var rings = new LinearRing[nRings];
        for (int r = 0; r < nRings; r++)
        {
            int n = (int)ReadUInt32(buf, ref pos, le);
            var seq = ReadSequence(buf, ref pos, le, n, dim, measures);
            rings[r] = _factory.CreateLinearRing(seq);
        }
        var holes = nRings > 1 ? rings.AsSpan(1).ToArray() : Array.Empty<LinearRing>();
        return _factory.CreatePolygon(rings[0], holes);
    }

    private Geometry ReadMulti(ReadOnlySpan<byte> buf, ref int pos, bool le, int kind, ref int capturedSrid)
    {
        int n = (int)ReadUInt32(buf, ref pos, le);
        switch (kind)
        {
            case 4:
            {
                var arr = new Point[n];
                for (int i = 0; i < n; i++) arr[i] = (Point)ReadGeometry(buf, ref pos, ref capturedSrid);
                return _factory.CreateMultiPoint(arr);
            }
            case 5:
            {
                var arr = new LineString[n];
                for (int i = 0; i < n; i++) arr[i] = (LineString)ReadGeometry(buf, ref pos, ref capturedSrid);
                return _factory.CreateMultiLineString(arr);
            }
            case 6:
            {
                var arr = new Polygon[n];
                for (int i = 0; i < n; i++) arr[i] = (Polygon)ReadGeometry(buf, ref pos, ref capturedSrid);
                return _factory.CreateMultiPolygon(arr);
            }
            default: throw new InvalidOperationException();
        }
    }

    private GeometryCollection ReadCollection(ReadOnlySpan<byte> buf, ref int pos, bool le, ref int capturedSrid)
    {
        int n = (int)ReadUInt32(buf, ref pos, le);
        var arr = new Geometry[n];
        for (int i = 0; i < n; i++) arr[i] = ReadGeometry(buf, ref pos, ref capturedSrid);
        return _factory.CreateGeometryCollection(arr);
    }

    private CoordinateSequence ReadSequence(ReadOnlySpan<byte> buf, ref int pos, bool le, int count, int dim, int measures)
    {
        int bytes = count * dim * 8;
        var packed = new double[count * dim];
        CopyCoords(buf.Slice(pos, bytes), packed, le);
        pos += bytes;
        return PackedSequenceBuilder.Create(_seqFactory, packed, dim, measures);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyCoords(ReadOnlySpan<byte> src, double[] dst, bool le)
    {
        if (le) CoordinateBlockReader.ReadLittleEndian(src, dst);
        else CoordinateBlockReader.ReadBigEndian(src, dst);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadUInt32(ReadOnlySpan<byte> buf, ref int pos, bool le)
    {
        var slice = buf.Slice(pos, 4);
        pos += 4;
        return le ? BinaryPrimitives.ReadUInt32LittleEndian(slice) : BinaryPrimitives.ReadUInt32BigEndian(slice);
    }
}
