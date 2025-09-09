using System;
using System.Collections.Generic;
using System.Linq;

namespace VolumeKeeper.Util;

public static class Util
{
    public static void DisposeAll(params IDisposable?[] disposables)
    {
        if (disposables.Length == 0) return;
        DisposeAll(disposables.AsEnumerable());
    }

    public static void DisposeAll(IEnumerable<IDisposable?> disposables)
    {
        var exceptions = new List<Exception>();

        foreach (var disposable in disposables)
        {
            if (disposable == null) continue;
            try { disposable.Dispose(); }
            catch (Exception ex) { exceptions.Add(ex); }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }
}
