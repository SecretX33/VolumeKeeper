using System;
using System.Collections.Generic;

namespace VolumeKeeper.Util;

public static class Util
{
    public static void DisposeAll(params IDisposable?[] disposables)
    {
        if (disposables.Length == 0) return;

        var exceptions = new List<Exception>();

        foreach (var d in disposables)
        {
            if (d == null) continue;
            try { d.Dispose(); }
            catch (Exception ex) { exceptions.Add(ex); }
        }

        if (exceptions.Count > 0)
            throw new AggregateException(exceptions);
    }
}
