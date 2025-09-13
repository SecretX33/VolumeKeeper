using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.UI.Xaml;

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
        this IDictionary<TKey, TValue> dict,
        TKey key
    ) where TKey : notnull => dict.TryGetValue(key, out var value) ? value : default;

    public static void AddAll<TKey, TValue>(
        this IDictionary<TKey, TValue> dict,
        IEnumerable<KeyValuePair<TKey, TValue>> items
    ) where TKey : notnull
    {
        foreach (var item in items)
        {
            dict[item.Key] = item.Value;
        }
    }
}
