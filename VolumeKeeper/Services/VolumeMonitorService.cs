using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VolumeKeeper.Services;

public class VolumeMonitorService : IDisposable
{
    private readonly AudioSessionManager _audioSessionManager;
    private readonly VolumeStorageService _storageService;
    private readonly ConcurrentDictionary<string, float> _lastKnownVolumes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _pollTimer;
    private readonly SemaphoreSlim _pollLock = new(1, 1);
    private volatile bool _isDisposed;

    public VolumeMonitorService(AudioSessionManager audioSessionManager, VolumeStorageService storageService)
    {
        _audioSessionManager = audioSessionManager;
        _storageService = storageService;
        _pollTimer = new Timer(PollVolumeChanges, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
    }

    private async void PollVolumeChanges(object? state)
    {
        if (_isDisposed || !await _pollLock.WaitAsync(0))
            return;

        try
        {
            var sessions = _audioSessionManager.GetAllSessions();

            foreach (var session in sessions)
            {
                var currentVolume = session.Volume;

                if (_lastKnownVolumes.TryGetValue(session.ExecutableName, out var lastVolume))
                {
                    if (Math.Abs(currentVolume - lastVolume) > 0.1f)
                    {
                        await SaveVolumeChange(session.ExecutableName, currentVolume);
                        _lastKnownVolumes[session.ExecutableName] = currentVolume;
                    }
                }
                else
                {
                    _lastKnownVolumes[session.ExecutableName] = currentVolume;
                    var savedVolume = await _storageService.GetVolumeAsync(session.ExecutableName);
                    if (savedVolume == null)
                    {
                        await SaveVolumeChange(session.ExecutableName, currentVolume);
                    }
                }
            }

            var currentExecutables =
                new HashSet<string>(sessions.Select(s => s.ExecutableName), StringComparer.OrdinalIgnoreCase);
            var toRemove = _lastKnownVolumes.Keys.Where(k => !currentExecutables.Contains(k)).ToList();
            foreach (var key in toRemove)
            {
                _lastKnownVolumes.TryRemove(key, out _);
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Error during volume monitoring poll", ex, "VolumeMonitorService");
        }
        finally
        {
            _pollLock.Release();
        }
    }

    private async Task SaveVolumeChange(string executableName, float volumePercentage)
    {
        try
        {
            await _storageService.SaveVolumeAsync(executableName, (int)Math.Round(volumePercentage));
            App.Logger.LogInfo($"Volume changed for {executableName}: {volumePercentage:F0}%", "VolumeMonitorService");
        }
        catch (Exception ex)
        {
            App.Logger.LogError($"Failed to save volume change for {executableName}", ex, "VolumeMonitorService");
        }
    }

    public Task InitializeAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var sessions = _audioSessionManager.GetAllSessions();

                foreach (var session in sessions)
                {
                    _lastKnownVolumes[session.ExecutableName] = session.Volume;
                }

                App.Logger.LogInfo($"Volume monitor initialized with {_lastKnownVolumes.Count} active sessions",
                    "VolumeMonitorService");
            }
            catch (Exception ex)
            {
                App.Logger.LogError("Failed to initialize volume monitor", ex, "VolumeMonitorService");
            }
        });
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _pollTimer.Dispose();
        _pollLock.Dispose();
    }
}
