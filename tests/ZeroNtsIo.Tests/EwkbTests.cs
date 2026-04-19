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
        // Why: NTS WKBWriter with handleSRID emits 1000-offset + 0x20000000 SRID flag (canonical EWKB).
        // These round through NTS → bytes → ZWkbReader and must match original coords + SRID.
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
        // Why: PostGIS 1.x encodes Z/M via type high bits instead of the OGC 1000 offset. NTS WKBWriter
        // never emits this form, so the bytes are hand-crafted; NTS WKBReader accepts them and is used
        // as oracle to confirm ZWkbReader produces the same geometry.
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
        // Why: close the round trip on the write side — ZWkbWriter with handleSRID must produce bytes
        // that NTS's WKBReader accepts and decodes with the same SRID + coords.
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
        // Why: default path must remain OGC ISO (no SRID bit, no 4-byte SRID field) — guard against
        // accidentally exporting EWKB for consumers that only understand OGC.
        var g = _wkt.Read("POINT (1 2)");
        g.SRID = 4326;
        byte[] ogc = _zWriter.Write(g, ByteOrder.LittleEndian); // handleSRID default false
        Assert.Equal(21, ogc.Length); // 1 bo + 4 type + 16 xy
        uint type = BinaryPrimitives.ReadUInt32LittleEndian(ogc.AsSpan(1, 4));
        Assert.Equal(0u, type & EwkbFlags.Any);
    }

    [Fact]
    public void ZWkbWriter_handleSRID_is_noop_when_srid_unset()
    {
        // Why: handleSRID=true with no meaningful SRID must not set the flag — NTS defaults to
        // SRID=-1 for factory-built geometries, so the gate is `srid > 0`. An SRID-flag without a
        // real SRID would confuse downstream EWKB readers.
        var g = _wkt.Read("POINT (1 2)");
        g.SRID = 0;
        byte[] bytes = _zWriter.Write(g, ByteOrder.LittleEndian, handleSRID: true);
        Assert.Equal(21, bytes.Length); // 1 bo + 4 type + 16 xy, no SRID field
        uint type = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(1, 4));
        Assert.Equal(0u, type & EwkbFlags.Srid);
    }

    [Fact]
    public void ZWkbWriter_handleSRID_multipolygon_root_only()
    {
        // Why: root carries the SRID flag; nested children must NOT repeat it (that's the PostGIS
        // convention NTS follows). Verify by scanning the byte stream for 0x20000000 patterns.
        var g = _wkt.Read("MULTIPOLYGON (((0 0, 1 0, 1 1, 0 0)), ((5 5, 6 5, 6 6, 5 5)))");
        g.SRID = 4326;
        byte[] bytes = _zWriter.Write(g, ByteOrder.LittleEndian, handleSRID: true);

        // Root type (bytes 1..5): MultiPolygon (6) with SRID flag set.
        uint rootType = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(1, 4));
        Assert.Equal(EwkbFlags.Srid | 6u, rootType);

        // Layout: bo(1) + type(4) + SRID(4) + numGeoms(4) = 13; child bo at offset 13, child type at 14.
        // Child type must be 3 (Polygon) with NO SRID flag.
        uint childType = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(14, 4));
        Assert.Equal(3u, childType);

        // NTS reads it back with correct SRID and coords.
        var round = _ntsReader.Read(bytes);
        Assert.Equal(4326, round.SRID);
        CoordinateAsserts.AssertCoordinatesBitEqual(g, round, ulpTolerance: 0);
    }

    [Fact]
    public void ZWkbReader_srid_propagates_into_multipolygon_children()
    {
        // Why: NTS's Geometry.SRID setter propagates SRID to children. Verify that after ZWkbReader
        // sets the root SRID, child polygons also expose it.
        var g = _wkt.Read("MULTIPOLYGON (((0 0, 1 0, 1 1, 0 0)), ((5 5, 6 5, 6 6, 5 5)))");
        g.SRID = 3857;
        byte[] ewkb = new WKBWriter(ByteOrder.LittleEndian, handleSRID: true, emitZ: false, emitM: false).Write(g);

        var round = _zReader.Read(ewkb);
        Assert.Equal(3857, round.SRID);
        for (int i = 0; i < round.NumGeometries; i++)
            Assert.Equal(3857, round.GetGeometryN(i).SRID);
    }

    // ---------- helpers ----------

    private static byte[] BuildHighBitPoint(uint typeHighBits, double[] coords, int? srid, bool le)
    {
        // Point base type = 1, combined with PostGIS legacy high-bit flag(s).
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
