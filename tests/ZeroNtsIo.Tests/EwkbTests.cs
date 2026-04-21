using System.Buffers.Binary;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using ZeroNtsIo.Internal;
using ZeroNtsIo.Reference;
using ZeroNtsIo.Tests.Fixtures;

namespace ZeroNtsIo.Tests;

public class EwkbTests
{
    private readonly NtsWktReader _wkt = new(Samples.Services);
    private readonly ZWkbReader _zReader = new(Samples.Services);
    private readonly ZWkbWriter _zWriter = new();
    private readonly WKBReader _ntsReader = new(Samples.Services);

    public static IEnumerable<object[]> SridSamples()
    {
        // Why: NTS の WKBWriter は handleSRID 付きで 1000 オフセット + 0x20000000 SRID フラグを出力する（正統な EWKB）。
        // NTS → バイト列 → ZWkbReader というラウンドトリップで、元の座標と SRID に一致する必要がある。
        yield return new object[] { "POINT (1 2)", 4326 };
        yield return new object[] { "POINT Z (1 2 3)", 4326 };
        yield return new object[] { "POINT M (1 2 3)", 3857 };
        yield return new object[] { "POINT ZM (1 2 3 4)", 4326 };
        yield return new object[] { "LINESTRING (0 0, 1 1, 2 4, -3 2)", 4326 };
        yield return new object[] { "LINESTRING Z (0 0 0, 1 1 1)", 4326 };
        yield return new object[] { "POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))", 4326 };
        yield return new object[] { "POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0), (2 2, 4 2, 4 4, 2 4, 2 2))", 4326 };
        yield return new object[] { "MULTIPOINT ((1 2), (3 4), (5 6))", 4326 };
        yield return new object[] { "MULTILINESTRING ((0 0, 1 1), (2 2, 3 3))", 4326 };
        yield return new object[] { "MULTIPOLYGON (((0 0, 1 0, 1 1, 0 0)), ((5 5, 6 5, 6 6, 5 5)))", 4326 };
        yield return new object[] { "GEOMETRYCOLLECTION (POINT (1 2), LINESTRING (0 0, 1 1))", 4326 };
    }

    [Theory]
    [MemberData(nameof(SridSamples))]
    public void Nts_ewkb_with_srid_is_read_back_correctly_LE(string wkt, int srid)
    {
        AssertEwkbRoundTrip(wkt, srid, ByteOrder.LittleEndian);
    }

    [Theory]
    [MemberData(nameof(SridSamples))]
    public void Nts_ewkb_with_srid_is_read_back_correctly_BE(string wkt, int srid)
    {
        AssertEwkbRoundTrip(wkt, srid, ByteOrder.BigEndian);
    }

    private void AssertEwkbRoundTrip(string wkt, int srid, ByteOrder bo)
    {
        var expected = _wkt.Read(wkt);
        expected.SRID = srid;
        var writer = new WKBWriter(bo, handleSRID: true, emitZ: AnyZ(expected), emitM: AnyM(expected));
        byte[] ewkb = writer.Write(expected);

        var actual = _zReader.Read(ewkb);
        Assert.Equal(srid, actual.SRID);
        CoordinateAsserts.AssertCoordinatesBitEqual(expected, actual, ulpTolerance: 0);
    }

    public static IEnumerable<object?[]> LegacyHighBitSamples()
    {
        // Why: PostGIS 1.x は OGC の 1000 オフセットではなく、タイプコードの高位ビットで Z/M をエンコードする。
        // NTS WKBWriter はこの形式を出力しないため、バイト列は手動で組み立てる。NTS WKBReader が受理するので、
        // それをオラクルとして ZWkbReader が同じジオメトリを返すことを確認する。
        yield return new object?[] { EwkbFlags.Z,           new[] { 1.0, 2.0, 3.0 },       (int?)null,  true,  false };
        yield return new object?[] { EwkbFlags.M,           new[] { 1.0, 2.0, 99.0 },      (int?)null,  false, true  };
        yield return new object?[] { EwkbFlags.Z | EwkbFlags.M, new[] { 1.0, 2.0, 3.0, 99.0 }, (int?)null, true,  true };
        yield return new object?[] { EwkbFlags.Srid | EwkbFlags.Z, new[] { 1.0, 2.0, 3.0 }, (int?)4326,  true,  false };
    }

    [Theory]
    [MemberData(nameof(LegacyHighBitSamples))]
    public void Legacy_postgis_highbit_point_is_accepted(uint typeHighBits, double[] coords, int? srid, bool expectZ, bool expectM)
    {
        byte[] wkb = BuildHighBitPoint(typeHighBits, coords, srid, le: true);
        var fromZ = _zReader.Read(wkb);
        var fromNts = _ntsReader.Read(wkb);
        CoordinateAsserts.AssertCoordinatesBitEqual(fromNts, fromZ, ulpTolerance: 0);
        Assert.Equal(fromNts.SRID, fromZ.SRID);

        var seq = ((Point)fromZ).CoordinateSequence;
        Assert.Equal(expectZ, seq.HasZ);
        Assert.Equal(expectM, seq.HasM);
    }

    [Fact]
    public void ZWkbWriter_handleSRID_emits_readable_ewkb()
    {
        // Why: 書き込み側のラウンドトリップを閉じる。ZWkbWriter を handleSRID 付きで呼んだ出力は、
        // NTS の WKBReader が受理し、同じ SRID と座標にデコードできなければならない。
        var g = _wkt.Read("POINT Z (1 2 3)");
        g.SRID = 4326;
        byte[] ewkb = _zWriter.Write(g, ByteOrder.LittleEndian, handleSRID: true);

        var viaNts = _ntsReader.Read(ewkb);
        Assert.Equal(4326, viaNts.SRID);
        CoordinateAsserts.AssertCoordinatesBitEqual(g, viaNts, ulpTolerance: 0);

        var viaZ = _zReader.Read(ewkb);
        Assert.Equal(4326, viaZ.SRID);
        CoordinateAsserts.AssertCoordinatesBitEqual(g, viaZ, ulpTolerance: 0);
    }

    [Fact]
    public void ZWkbWriter_without_handleSRID_stays_ogc_iso()
    {
        // Why: 既定経路は OGC ISO のままでなければならない（SRID ビットも 4 バイトの SRID フィールドも無し）。
        // OGC しか解釈できない消費者に、意図せず EWKB を出してしまうことを防ぐ。
        var g = _wkt.Read("POINT (1 2)");
        g.SRID = 4326;
        byte[] ogc = _zWriter.Write(g, ByteOrder.LittleEndian); // handleSRID は既定 false
        Assert.Equal(21, ogc.Length); // byte order(1) + type(4) + xy(16)
        uint type = BinaryPrimitives.ReadUInt32LittleEndian(ogc.AsSpan(1, 4));
        Assert.Equal(0u, type & EwkbFlags.Any);
    }

    [Fact]
    public void ZWkbWriter_handleSRID_is_noop_when_srid_unset()
    {
        // Why: handleSRID=true でも有効な SRID が無い場合はフラグを立ててはならない。
        // NTS は factory 生成ジオメトリの SRID を -1 とするため、ゲートは `srid > 0` とする。
        // 実 SRID が無いのに SRID フラグだけ立てると、下流の EWKB Reader を混乱させる。
        var g = _wkt.Read("POINT (1 2)");
        g.SRID = 0;
        byte[] bytes = _zWriter.Write(g, ByteOrder.LittleEndian, handleSRID: true);
        Assert.Equal(21, bytes.Length); // byte order(1) + type(4) + xy(16)、SRID フィールドは無い
        uint type = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(1, 4));
        Assert.Equal(0u, type & EwkbFlags.Srid);
    }

    [Fact]
    public void ZWkbWriter_handleSRID_multipolygon_root_only()
    {
        // Why: SRID フラグはルートにのみ付ける。子では繰り返さない（NTS が追随する PostGIS の慣習）。
        // バイト列を走査して 0x20000000 のパターンを確認する。
        var g = _wkt.Read("MULTIPOLYGON (((0 0, 1 0, 1 1, 0 0)), ((5 5, 6 5, 6 6, 5 5)))");
        g.SRID = 4326;
        byte[] bytes = _zWriter.Write(g, ByteOrder.LittleEndian, handleSRID: true);

        // ルートタイプ（バイト 1..5）は MultiPolygon (6) + SRID フラグ。
        uint rootType = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(1, 4));
        Assert.Equal(EwkbFlags.Srid | 6u, rootType);

        // レイアウト: bo(1) + type(4) + SRID(4) + numGeoms(4) = 13 バイト。子の byte order はオフセット 13、type は 14。
        // 子のタイプは 3 (Polygon) で、SRID フラグは立っていてはならない。
        uint childType = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(14, 4));
        Assert.Equal(3u, childType);

        // NTS が正しい SRID と座標で読み戻せることを確認する。
        var round = _ntsReader.Read(bytes);
        Assert.Equal(4326, round.SRID);
        CoordinateAsserts.AssertCoordinatesBitEqual(g, round, ulpTolerance: 0);
    }

    [Fact]
    public void ZWkbReader_srid_propagates_into_multipolygon_children()
    {
        // Why: NTS の Geometry.SRID setter は子にも SRID を伝播させる。
        // ZWkbReader がルートに SRID をセットした後、子の Polygon にも同じ SRID が反映されることを確認する。
        var g = _wkt.Read("MULTIPOLYGON (((0 0, 1 0, 1 1, 0 0)), ((5 5, 6 5, 6 6, 5 5)))");
        g.SRID = 3857;
        byte[] ewkb = new WKBWriter(ByteOrder.LittleEndian, handleSRID: true, emitZ: false, emitM: false).Write(g);

        var round = _zReader.Read(ewkb);
        Assert.Equal(3857, round.SRID);
        for (int i = 0; i < round.NumGeometries; i++)
            Assert.Equal(3857, round.GetGeometryN(i).SRID);
    }

    // ---------- ヘルパー ----------

    private static byte[] BuildHighBitPoint(uint typeHighBits, double[] coords, int? srid, bool le)
    {
        // Point のベースタイプ = 1 に、PostGIS レガシーの高位ビットフラグを合わせる。
        uint type = typeHighBits | 1u;
        if (srid.HasValue) type |= EwkbFlags.Srid;

        int size = 1 + 4 + (srid.HasValue ? 4 : 0) + coords.Length * 8;
        var buf = new byte[size];
        int p = 0;
        buf[p++] = (byte)(le ? 1 : 0);
        WriteU32(buf.AsSpan(p, 4), type, le); p += 4;
        if (srid.HasValue) { WriteU32(buf.AsSpan(p, 4), (uint)srid.Value, le); p += 4; }
        foreach (double d in coords)
        {
            if (le) BinaryPrimitives.WriteDoubleLittleEndian(buf.AsSpan(p, 8), d);
            else BinaryPrimitives.WriteDoubleBigEndian(buf.AsSpan(p, 8), d);
            p += 8;
        }
        return buf;
    }

    private static void WriteU32(Span<byte> dst, uint v, bool le)
    {
        if (le) BinaryPrimitives.WriteUInt32LittleEndian(dst, v);
        else BinaryPrimitives.WriteUInt32BigEndian(dst, v);
    }

    private static bool AnyZ(Geometry g) => AnySeq(g, s => s.HasZ);
    private static bool AnyM(Geometry g) => AnySeq(g, s => s.HasM);

    private static bool AnySeq(Geometry g, Func<CoordinateSequence, bool> pred)
    {
        if (g is MultiPoint or MultiLineString or MultiPolygon or GeometryCollection)
        {
            for (int i = 0; i < g.NumGeometries; i++)
                if (AnySeq(g.GetGeometryN(i), pred)) return true;
            return false;
        }
        return g switch
        {
            Point p => !p.IsEmpty && pred(p.CoordinateSequence),
            LineString ls => !ls.IsEmpty && pred(ls.CoordinateSequence),
            Polygon poly => !poly.IsEmpty && pred(poly.ExteriorRing.CoordinateSequence),
            _ => false,
        };
    }
}
