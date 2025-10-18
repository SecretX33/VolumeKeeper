using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using NAudio.CoreAudioApi;
using VolumeKeeper.Services.Log;

namespace VolumeKeeper.Util;

public static class Extensions
{
    private static Logger Logger => App.Logger.Named();

    public static void ShowAndFocus(this Window window)
    {
        window = window ?? throw new ArgumentNullException(nameof(window));
        window.DispatcherQueue.TryEnqueueImmediate(() => NativeMethods.ShowAndFocus(window));
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

    public static Task<T> TryFetchImmediate<T>(
        this DispatcherQueue queue,
        Func<T> function
    )
    {
        var tcs = new TaskCompletionSource<T>();

        queue.TryEnqueueImmediate(() =>
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

        return tcs.Task;
    }

    public static async Task<T> TryFetchImmediate<T>(
        this DispatcherQueue queue,
        Func<Task<T>> asyncFunction
    )
    {
        var tcs = new TaskCompletionSource<T>();

        queue.TryEnqueueImmediate(async void () =>
        {
            try
            {
                var result = await asyncFunction();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

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

    public static IReadOnlyList<AudioSessionControl> Sessions(
        this AudioSessionManager manager
    )
    {
        var sessionCollection = manager.Sessions;
        if (sessionCollection == null || sessionCollection.Count == 0)
        {
            return ImmutableList<AudioSessionControl>.Empty;
        }
        return Enumerable.Range(0, sessionCollection.Count)
            .Select(i => sessionCollection[i])
            .ToList();
    }

    public static IReadOnlyList<AudioSessionControl> FreshSessions(
        this AudioSessionManager manager
    ) {
        manager.RefreshSessions();
        return manager.Sessions();
    }

    public static uint? GetProcessIdOrNull(
        this AudioSessionControl session
    ) {
        try
        {
            return session.GetProcessID;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
