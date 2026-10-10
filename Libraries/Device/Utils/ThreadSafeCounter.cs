using System.Threading;

namespace UniversalDeviceToolkit.Lib.Utils;

public class ThreadSafeCounter
{
    private int _value;

    public int Value => Volatile.Read(ref _value);

    public void Increment() => Interlocked.Increment(ref _value);

    public void Decrement()
    {
        while (true)
        {
            var current = Volatile.Read(ref _value);
            if (current <= 0)
                return;

            if (Interlocked.CompareExchange(ref _value, current - 1, current) == current)
                return;
        }
    }

    public void Reset() => Interlocked.Exchange(ref _value, 0);
}
