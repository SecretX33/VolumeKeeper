using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Graphics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace VolumeKeeper.Util;

public static class Extensions
{
    public static void ShowAndFocus(this Window window)
    {
        window = window ?? throw new ArgumentNullException(nameof(window));
        window.DispatcherQueue.TryEnqueueImmediate(() => NativeMethods.ShowAndFocus(window));
    }

    public static void SetMinMaxSize(
        this Window window,
        PointInt32? minWindowSize = null,
        PointInt32? maxWindowSize = null
    )
    {
        try
        {
            var helper = new Win32WindowHelper(window);
            helper.SetWindowMinMaxSize(minWindowSize, maxWindowSize);
        } catch (Exception ex)
        {
            App.Logger.LogWarning("Failed to set window min/max size.", ex, "Extensions");
        }
    }

    public static TValue? GetOrNull<TKey, TValue>(
        this IDictionary<TKey, TValue> dict,
        TKey key
    ) where TKey : notnull where TValue : class => dict.TryGetValue(key, out var value) ? value : null;

    public static TValue? GetOrNullValue<TKey, TValue>(
        this IDictionary<TKey, TValue> dict,
        TKey key
    ) where TKey : notnull where TValue : struct => dict.TryGetValue(key, out var value) ? value : null;

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

    public static async Task<T> TryFetch<T>(
        this DispatcherQueue queue,
        Func<T> function
    )
    {
        var tcs = new TaskCompletionSource<T>();

        var added = queue.TryEnqueue(() =>
        {
            try
            {
                var result = function();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        if (!added)
        {
            throw new InvalidOperationException("Failed to enqueue operation to DispatcherQueue.");
        }

        return await tcs.Task;
    }

    public static void TryEnqueueImmediate(
        this DispatcherQueue queue,
        DispatcherQueueHandler callback
    )
    {
        var success = true;

        // Directly invoke the callback if we're already on the correct thread, else enqueue it
        if (queue.HasThreadAccess) callback.Invoke();
        else success = queue.TryEnqueue(callback);

        if (!success)
        {
            throw new InvalidOperationException("Failed to enqueue operation to DispatcherQueue.");
        }
    }
}
