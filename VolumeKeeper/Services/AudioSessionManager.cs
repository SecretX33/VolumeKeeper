using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using VolumeKeeper.Models;
using static VolumeKeeper.Util.Util;

namespace VolumeKeeper.Services;

public class AudioSessionManager : IDisposable
{
    private const int VolumeDebounceDelayMs = 300;
    private readonly MMDeviceEnumerator _deviceEnumerator;
    private MMDevice? _defaultDevice;
    private SessionCollection? _sessions;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _volumeDebounceTokens = new();
    private readonly SemaphoreSlim _volumeSetSemaphore = new(1, 1);

    public AudioSessionManager()
    {
        _deviceEnumerator = new MMDeviceEnumerator();
        RefreshDevice();
    }

    private void RefreshDevice()
    {
        try
        {
            _defaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _sessions = _defaultDevice?.AudioSessionManager?.Sessions;
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to get default audio device", ex, "AudioSessionManager");
        }
    }

    public List<AudioSession> GetAllSessions()
    {
        var sessions = new List<AudioSession>();
        RefreshDevice();

        if (_sessions == null)
            return sessions;

        try
        {
            for (int i = 0; i < _sessions.Count; i++)
            {
                var sessionControl = _sessions[i];
                if (sessionControl == null)
                    continue;

                var simpleVolume = sessionControl.SimpleAudioVolume;
                if (simpleVolume == null)
                    continue;

                var session = CreateAudioSession(sessionControl, simpleVolume);
                if (session != null && !string.IsNullOrEmpty(session.ExecutableName))
                {
                    sessions.Add(session);
                }
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to enumerate audio sessions", ex, "AudioSessionManager");
        }

        return sessions;
    }

    private AudioSession? CreateAudioSession(AudioSessionControl sessionControl, SimpleAudioVolume simpleVolume)
    {
        try
        {
            var processId = (int)sessionControl.GetProcessID;
            if (processId == 0)
                return null;

            using var process = Process.GetProcessById(processId);
            var executableName = Path.GetFileName(process.MainModule?.FileName ?? process.ProcessName);

            return new AudioSession
            {
                ProcessId = processId,
                ProcessName = process.ProcessName,
                ExecutableName = executableName,
                Volume = simpleVolume.Volume * 100,
                IsMuted = simpleVolume.Mute,
                IconPath = sessionControl.IconPath ?? string.Empty,
                SessionControl = sessionControl
            };
        }
        catch
        {
            return null;
        }
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
        var sessions = GetAllSessions();
        var matchingSessions = sessions.Where(s =>
            string.Equals(s.ExecutableName, executableName, StringComparison.OrdinalIgnoreCase));

        bool anySet = false;
        foreach (var session in matchingSessions)
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
        var sessions = GetAllSessions();
        var session = sessions.FirstOrDefault(s =>
            string.Equals(s.ExecutableName, executableName, StringComparison.OrdinalIgnoreCase));

        return session?.Volume;
    }

    public void Dispose()
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
                .Concat(new IDisposable?[] { _volumeSetSemaphore, _deviceEnumerator })
        );
    }
}
