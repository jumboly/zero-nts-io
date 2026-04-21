using System.Buffers;
using NetTopologySuite.Geometries;

namespace ZeroNtsIo.Stages.Internal;

/// <summary>
/// <see cref="ArrayPool{T}"/> を利用した可変長 <see cref="Coordinate"/> バッファ。
/// 最後の <see cref="Finish"/> 呼び出しで要素数ぴったりの配列を返しつつ pool バッファを解放するため、
/// 呼び出し側は返却された座標配列のライフタイム中、1 つの確保しか保持しない。
/// </summary>
internal struct PooledCoordinateBuffer : IDisposable
{
    private Coordinate[] _buf;
    private int _count;

    public PooledCoordinateBuffer(int initialCapacity)
    {
        _buf = ArrayPool<Coordinate>.Shared.Rent(initialCapacity);
        _count = 0;
    }

    public int Count => _count;

    public void Add(Coordinate c)
    {
        if (_count == _buf.Length) Grow();
        _buf[_count++] = c;
    }

    private void Grow()
    {
        var next = ArrayPool<Coordinate>.Shared.Rent(_buf.Length * 2);
        Array.Copy(_buf, next, _count);
        ArrayPool<Coordinate>.Shared.Return(_buf, clearArray: true);
        _buf = next;
    }

    /// <summary>要素数ぴったりの配列にコピーしつつ pool バッファを返却する。戻り値の配列が唯一の live な確保となる。</summary>
    public Coordinate[] Finish()
    {
        var result = new Coordinate[_count];
        Array.Copy(_buf, result, _count);
        ArrayPool<Coordinate>.Shared.Return(_buf, clearArray: true);
        _buf = Array.Empty<Coordinate>();
        _count = 0;
        return result;
    }

    public void Dispose()
    {
        if (_buf.Length > 0)
        {
            ArrayPool<Coordinate>.Shared.Return(_buf, clearArray: true);
            _buf = Array.Empty<Coordinate>();
        }
    }
}
