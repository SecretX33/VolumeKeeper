using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using VolumeKeeper.Models;
using VolumeKeeper.Services.Managers;
using VolumeKeeper.Util;
using static VolumeKeeper.Util.Util;

namespace VolumeKeeper.Services;

public partial class AudioSessionService(
    AudioSessionManager sessionManager
) : IDisposable
{
    private const int VolumeDebounceDelayMs = 300;
    private readonly ConcurrentDictionary<VolumeApplicationId, CancellationTokenSource> _volumeDebounceTokens = new();
    private readonly SemaphoreSlim _volumeSetSemaphore = new(1, 1);
    private readonly AtomicReference<bool> _isDisposed = new(false);
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    public Task<bool> SetSessionVolumeAsync(VolumeApplicationId volumeApplicationId, int volumePercentage)
    {
        return Task.Run(async () =>
        {
            // Create new cancellation token for this debounce
            using var cancellationTokenSource = _volumeDebounceTokens.AddOrUpdate(volumeApplicationId, _ => new CancellationTokenSource(), (_, oldValue) =>
            {
                oldValue.Cancel();
                return new CancellationTokenSource();
            });
            var cancellationToken = cancellationTokenSource.Token;

            try
            {
                // Wait for debounce period
                await Task.Delay(VolumeDebounceDelayMs, cancellationToken).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested) return false;

                else
                {
                    await _volumeSetSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        return await _dispatcherQueue.TryFetch(() => SetSessionVolumeImmediate(volumeApplicationId, volumePercentage))
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        _volumeSetSemaphore.Release();
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // Debounce was cancelled, this is expected
            }
            catch (Exception ex)
            {
                App.Logger.LogError($"Error setting volume for {volumeApplicationId}", ex, "AudioSessionManager");
            }
            finally
            {
                _volumeDebounceTokens.TryRemove(new KeyValuePair<VolumeApplicationId, CancellationTokenSource>(volumeApplicationId, cancellationTokenSource));
            }

            return false;
        });
    }

    public bool SetSessionVolumeImmediate(VolumeApplicationId volumeApplicationId, int volumePercentage)
    {
        if (!_dispatcherQueue.HasThreadAccess)
            throw new InvalidOperationException($"{nameof(SetSessionVolumeImmediate)} must be called on the UI thread");

        var session = sessionManager.GetSessionById(volumeApplicationId);
        if (session == null)
        {
            App.Logger.LogWarning($"No audio session found for {volumeApplicationId}", "AudioSessionManager");
            return false;
        }

        bool anySet = false;
        try
        {
            session.Volume = volumePercentage;
            anySet = true;
            App.Logger.LogInfo($"Set volume for {volumeApplicationId} (PID: {session.ProcessId}) to {volumePercentage}%",
                "AudioSessionManager");
        }
        catch (Exception ex)
        {
            App.Logger.LogError($"Failed to set volume for {volumeApplicationId} (PID: {session.ProcessId})", ex,
                "AudioSessionManager");
        }

        return anySet;
    }

    public Task<bool> SetMuteSessionImmediateAsync(VolumeApplicationId volumeApplicationId, bool mute)
    {
        return Task.Run(() =>
        {
            var session = sessionManager.GetSessionById(volumeApplicationId);
            if (session == null)
            {
                App.Logger.LogWarning($"No audio session found for {volumeApplicationId}", "AudioSessionManager");
                return false;
            }

            bool anySet = false;
            try
            {
                session.IsMuted = mute;
                anySet = true;
                App.Logger.LogInfo($"Set mute for {volumeApplicationId} (PID: {session.ProcessId}) to {(mute ? "muted" : "unmuted")}",
                    "AudioSessionManager");
            }
            catch (Exception ex)
            {
                App.Logger.LogError($"Failed to set volume for {volumeApplicationId} (PID: {session.ProcessId})", ex,
                    "AudioSessionManager");
            }

            return anySet;
        });
    }

    public void Dispose()
    {
        if (!_isDisposed.CompareAndSet(false, true))
            return;

        try
        {
            // Clean up all debounce tokens
            foreach (var kvp in _volumeDebounceTokens)
            {
                try
                {
                    kvp.Value.Cancel();
                }
                catch
                {
                    App.Logger.LogDebug($"Exception was throw when attempting to cancel debounce token for {kvp.Key}", "AudioSessionManager");
                }
            }

            var tokensToDispose = _volumeDebounceTokens.Values.ToList();
            _volumeDebounceTokens.Clear();

            DisposeAll(
                tokensToDispose
                    .Concat(new IDisposable?[] { _volumeSetSemaphore })
            );
        }
        catch
        {
            /* Ignore exceptions during dispose */
        }

        GC.SuppressFinalize(this);
    }
}
