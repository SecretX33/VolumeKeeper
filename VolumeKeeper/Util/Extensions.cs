using System;
using System.Collections.Concurrent;
using Microsoft.UI.Xaml;
using VolumeKeeper.Interop;

namespace VolumeKeeper.Util;

public static class Extensions
{
    public static void ShowAndFocus(this Window window)
    {
        window = window ?? throw new ArgumentNullException(nameof(window));

        // Ensure the operation runs on the UI thread
        if (window.DispatcherQueue.HasThreadAccess)
        {
            NativeMethods.ShowAndFocus(window);
        }
        else
        {
            window.DispatcherQueue.TryEnqueue(() => NativeMethods.ShowAndFocus(window));
        }
    }

    public static TValue? GetOrNull<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dict,
        TKey key
    ) where TKey : notnull where TValue : class
    {
        return dict.TryGetValue(key, out var value) ? value : null;
    }
}
