using System.Buffers;
using NetTopologySuite.Geometries;

namespace FastNtsWk.Fast.Internal;

/// <summary>
/// Growable <see cref="Coordinate"/> buffer backed by <see cref="ArrayPool{T}"/>.
/// The final <see cref="Finish"/> call returns a right-sized array and releases the pool buffer,
/// so the caller owns only one allocation for the lifetime of the returned coordinates.
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

    /// <summary>Copy into a right-sized array and return the pool buffer. The returned array is the only live alloc.</summary>
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
