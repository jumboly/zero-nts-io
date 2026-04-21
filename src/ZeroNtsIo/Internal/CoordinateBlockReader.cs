using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace ZeroNtsIo.Internal;

/// <summary>
/// WKB 座標ブロックを <c>double[]</c> へコピーする高速経路。
/// LE はコスト 0 の再解釈、BE は <see cref="Vector128.Shuffle"/> による 16 バイト単位の SIMD バイトスワップ。
/// </summary>
internal static class CoordinateBlockReader
{
    // Why: Vector128.Shuffle はレーン毎のバイト並び替えを行う。このマスクは 16 バイトベクタの
    // 前半・後半それぞれ 8 バイト内でバイト順を反転させるため、1 回の呼び出しで packed double 2 個分の
    // バイト順を入れ替えられる。
    private static readonly Vector128<byte> SwapDoubleMask = Vector128.Create(
        (byte)7, 6, 5, 4, 3, 2, 1, 0,
              15, 14, 13, 12, 11, 10, 9, 8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadLittleEndian(ReadOnlySpan<byte> src, Span<double> dst)
    {
        // Why: little-endian マシン上では WKB-LE のバイトレイアウトがそのまま
        // PackedDoubleCoordinateSequence のメモリレイアウトと一致するため、raw バイトコピーだけで十分。
        src.Slice(0, dst.Length * sizeof(double)).CopyTo(MemoryMarshal.AsBytes(dst));
    }

    public static void ReadBigEndian(ReadOnlySpan<byte> src, Span<double> dst)
    {
        var srcBytes = src.Slice(0, dst.Length * sizeof(double));
        var dstBytes = MemoryMarshal.AsBytes(dst);
        int i = 0;
        int total = dstBytes.Length;

        if (Vector128.IsHardwareAccelerated)
        {
            while (i + 16 <= total)
            {
                var v = Vector128.Create<byte>(srcBytes.Slice(i, 16));
                var shuffled = Vector128.Shuffle(v, SwapDoubleMask);
                shuffled.CopyTo(dstBytes.Slice(i, 16));
                i += 16;
            }
        }

        while (i + 8 <= total)
        {
            ulong v = BinaryPrimitives.ReadUInt64LittleEndian(srcBytes.Slice(i, 8));
            BinaryPrimitives.WriteUInt64LittleEndian(dstBytes.Slice(i, 8), BinaryPrimitives.ReverseEndianness(v));
            i += 8;
        }
    }

    public static void WriteLittleEndian(ReadOnlySpan<double> src, Span<byte> dst)
    {
        MemoryMarshal.AsBytes(src).CopyTo(dst.Slice(0, src.Length * sizeof(double)));
    }

    public static void WriteBigEndian(ReadOnlySpan<double> src, Span<byte> dst)
    {
        var srcBytes = MemoryMarshal.AsBytes(src);
        var dstBytes = dst.Slice(0, src.Length * sizeof(double));
        int i = 0;
        int total = dstBytes.Length;

        if (Vector128.IsHardwareAccelerated)
        {
            while (i + 16 <= total)
            {
                var v = Vector128.Create<byte>(srcBytes.Slice(i, 16));
                var shuffled = Vector128.Shuffle(v, SwapDoubleMask);
                shuffled.CopyTo(dstBytes.Slice(i, 16));
                i += 16;
            }
        }

        while (i + 8 <= total)
        {
            ulong v = BinaryPrimitives.ReadUInt64LittleEndian(srcBytes.Slice(i, 8));
            BinaryPrimitives.WriteUInt64LittleEndian(dstBytes.Slice(i, 8), BinaryPrimitives.ReverseEndianness(v));
            i += 8;
        }
    }
}
