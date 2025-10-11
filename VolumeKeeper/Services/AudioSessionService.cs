using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using VolumeKeeper.Models;
using VolumeKeeper.Services.Log;
using VolumeKeeper.Util;
using static VolumeKeeper.Util.Util;
using AudioSessionManager = VolumeKeeper.Services.Managers.AudioSessionManager;

namespace VolumeKeeper.Services;

public sealed partial class AudioSessionService(
    AudioSessionManager sessionManager,
    DispatcherQueue mainThreadQueue
) : IDisposable
{
    private readonly Logger _logger = App.Logger.Named();
    private const int VolumeDebounceDelayMs = 300;
    private readonly ConcurrentDictionary<VolumeApplicationId, CancellationTokenSource> _volumeDebounceTokens = new();
    private readonly SemaphoreSlim _volumeSetSemaphore = new(1, 1);
    private readonly AtomicReference<bool> _isDisposed = new(false);

    public Task<bool> SetSessionVolumeAsync(VolumeApplicationId volumeApplicationId, int volumePercentage)
    {
        // Create new cancellation token for this debounce
        var cancellationTokenSource = _volumeDebounceTokens.AddOrUpdate(volumeApplicationId, _ => new CancellationTokenSource(), (_, oldValue) =>
        {
            oldValue.Cancel();
            return new CancellationTokenSource();
        });
        var cancellationToken = cancellationTokenSource.Token;

        return Task.Run(async () =>
        {
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
                        return await mainThreadQueue.TryFetchImmediate(() => SetSessionVolumeImmediate(volumeApplicationId, volumePercentage))
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
                _logger.Error($"Error setting volume for {volumeApplicationId}", ex);
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
        RequireMainThreadAccess();

        var session = sessionManager.GetSessionById(volumeApplicationId);
        if (session == null)
        {
            _logger.Warn($"No audio session found for {volumeApplicationId}, could not set volume to {volumePercentage}");
            return false;
        }

        try
        {
            session.SetVolume(volumePercentage);
            _logger.Info($"Set volume for {session.ExecutableName} (PID: {session.ProcessId}) to {volumePercentage}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to set volume for {session.ExecutableName} (PID: {session.ProcessId})", ex);
            return false;
        }
    }

    public bool SetMuteSessionImmediate(VolumeApplicationId volumeApplicationId, bool mute)
    {
        RequireMainThreadAccess();

        var session = sessionManager.GetSessionById(volumeApplicationId);
        if (session == null)
        {
            _logger.Warn($"No audio session found for {volumeApplicationId}, could not set mute to {mute}");
            return false;
        }

        try
        {
            session.IsMuted = mute;
            _logger.Info($"Set mute for {volumeApplicationId} (PID: {session.ProcessId}) to {(mute ? "muted" : "unmuted")}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to set volume for {volumeApplicationId} (PID: {session.ProcessId})", ex);
            return false;
        }
    }

    private void RequireMainThreadAccess([CallerMemberName] string caller = "")
    {
        if (!mainThreadQueue.HasThreadAccess)
            throw new InvalidOperationException($"{caller} must be called on the UI thread");
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
                    _logger.Debug($"Exception was throw when attempting to cancel debounce token for {kvp.Key}");
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
    }
}
