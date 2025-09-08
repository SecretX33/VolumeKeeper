using System.Collections.Generic;

namespace VolumeKeeper.Util;

using System;
using System.Threading;

public class AtomicReference<T>(T value) {
    private T _value = value;
    private const int MaxUpdateAttempts = 100;

    // Get current value
    public T Get() => _value;

    // Set a new value atomically
    public void Set(T value) => Interlocked.Exchange(ref _value, value);

    // Get current value and set new value
    public T GetAndSet(T newValue) => Interlocked.Exchange(ref _value, newValue);

    // Set new value and return new value
    public T SetAndGet(T newValue)
    {
        Interlocked.Exchange(ref _value, newValue);
        return newValue;
    }

    // Compare-and-set
    public bool CompareAndSet(T expected, T newValue) =>
        EqualityComparer<T>.Default.Equals(Interlocked.CompareExchange(ref _value, newValue, expected), expected);

    // Atomically update the value using a function
    public T UpdateAndSet(Func<T, T> updater)
    {
        int attempt = 0;
        while (attempt < MaxUpdateAttempts)
        {
            var oldValue = _value;
            var newValue = updater(oldValue);

            if (!CompareAndSet(oldValue, newValue))
            {
                // Someone else updated it, retry
                attempt++;
                continue;
            }
            return newValue;
        }
        throw new InvalidOperationException("Failed to update value after maximum attempts");
    }
}

