using System.Buffers;
using System.Runtime.CompilerServices;

namespace ZeroNtsIo.Internal;

/// <summary><see cref="ArrayPool{T}"/> を利用した可変長 <c>double[]</c> バッファ。</summary>
internal struct PooledDoubleBuffer : IDisposable
{
    private double[] _buf;
    private int _count;

    public PooledDoubleBuffer(int initialCapacity)
    {
        _buf = ArrayPool<double>.Shared.Rent(initialCapacity);
        _count = 0;
    }

    public int Count => _count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendXY(double x, double y)
    {
        EnsureCapacity(_count + 2);
        _buf[_count++] = x;
        _buf[_count++] = y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendXYZ(double x, double y, double z)
    {
        EnsureCapacity(_count + 3);
        _buf[_count++] = x;
        _buf[_count++] = y;
        _buf[_count++] = z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendXYZM(double x, double y, double z, double m)
    {
        EnsureCapacity(_count + 4);
        _buf[_count++] = x;
        _buf[_count++] = y;
        _buf[_count++] = z;
        _buf[_count++] = m;
    }

    private void EnsureCapacity(int needed)
    {
        if (needed <= _buf.Length) return;
        int newCap = Math.Max(_buf.Length * 2, needed);
        var next = ArrayPool<double>.Shared.Rent(newCap);
        Array.Copy(_buf, next, _count);
        ArrayPool<double>.Shared.Return(_buf, clearArray: false);
        _buf = next;
    }

    public double[] Finish()
    {
        var result = new double[_count];
        Array.Copy(_buf, result, _count);
        ArrayPool<double>.Shared.Return(_buf, clearArray: false);
        _buf = Array.Empty<double>();
        _count = 0;
        return result;
    }

    public void Dispose()
    {
        if (_buf.Length > 0)
        {
            ArrayPool<double>.Shared.Return(_buf, clearArray: false);
            _buf = Array.Empty<double>();
        }
    }
}
