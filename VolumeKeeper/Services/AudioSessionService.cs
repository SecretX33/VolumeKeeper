using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VolumeKeeper.Services.Managers;
using VolumeKeeper.Util;
using static VolumeKeeper.Util.Util;

namespace VolumeKeeper.Services;

public partial class AudioSessionService : IDisposable
{
    private const int VolumeDebounceDelayMs = 300;
    private readonly AudioSessionManager _sessionManager;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _volumeDebounceTokens = new();
    private readonly SemaphoreSlim _volumeSetSemaphore = new(1, 1);
    private readonly AtomicReference<bool> _isDisposed = new(false);

    public AudioSessionService(AudioSessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public Task<bool> SetSessionVolume(string executableName, int volumePercentage)
    {
        return Task.Run(async () =>
        {
            var cacheKey = executableName.ToLowerInvariant();

            // Create new cancellation token for this debounce
            using var cancellationTokenSource = _volumeDebounceTokens.AddOrUpdate(cacheKey, _ => new CancellationTokenSource(), (_, oldValue) =>
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
                        return SetSessionVolumeImmediate(executableName, volumePercentage);
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
                App.Logger.LogError($"Error setting volume for {executableName}", ex, "AudioSessionManager");
            }
            finally
            {
                _volumeDebounceTokens.TryRemove(new KeyValuePair<string, CancellationTokenSource>(cacheKey, cancellationTokenSource));
            }

            return false;
        });
    }

    public bool SetSessionVolumeImmediate(string executableName, int volumePercentage)
    {
        var sessions = _sessionManager.GetSessionsByExecutable(executableName);

        bool anySet = false;
        foreach (var session in sessions)
        {
            try
            {
                session.SessionControl.SimpleAudioVolume.Volume = volumePercentage / 100f;
                anySet = true;
                App.Logger.LogInfo($"Set volume for {executableName} (PID: {session.ProcessId}) to {volumePercentage}%", "AudioSessionManager");
            }
            catch (Exception ex)
            {
                App.Logger.LogError($"Failed to set volume for {executableName} (PID: {session.ProcessId})", ex, "AudioSessionManager");
            }
        }

        return anySet;
    }

    public float? GetSessionVolume(string executableName)
    {
        var sessions = _sessionManager.GetSessionsByExecutable(executableName);
        var session = sessions.FirstOrDefault();
        return session?.Volume;
    }

    public void Dispose()
    {
        if (!_isDisposed.CompareAndSet(false, true))
            return;

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
}
