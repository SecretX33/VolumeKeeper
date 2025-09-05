using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VolumeKeeper.Services;

public class VolumeRestorationService : IDisposable
{
    private const int DelayBeforeRestoreMs = 500;
    private readonly AudioSessionManager _audioSessionManager;
    private readonly VolumeStorageService _storageService;
    private readonly ApplicationMonitorService _appMonitorService;
    private readonly ConcurrentDictionary<string, DateTime> _recentRestorations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _restorationCooldown = TimeSpan.FromSeconds(1);
    private bool _isDisposed;

    public VolumeRestorationService(
        AudioSessionManager audioSessionManager,
        VolumeStorageService storageService,
        ApplicationMonitorService appMonitorService)
    {
        _audioSessionManager = audioSessionManager;
        _storageService = storageService;
        _appMonitorService = appMonitorService;
        _appMonitorService.ApplicationLaunched += OnApplicationLaunched;
        _cleanupTimer = new Timer(CleanupOldRestorations, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    private async void OnApplicationLaunched(object? sender, ApplicationLaunchEventArgs e)
    {
        if (string.IsNullOrEmpty(e.ExecutableName))
            return;

        if (_recentRestorations.TryGetValue(e.ExecutableName, out var lastRestoration))
        {
            if (DateTime.UtcNow - lastRestoration < _restorationCooldown)
            {
                App.Logger.LogInfo($"Skipping volume restoration for {e.ExecutableName} (cooldown active)", "VolumeRestorationService");
                return;
            }
        }

        await Task.Delay(DelayBeforeRestoreMs);

        await RestoreVolumeAsync(e.ExecutableName);
    }

    public Task RestoreVolumeAsync(string executableName)
    {
        return Task.Run(async () =>
        {
            try
            {
                var savedVolume = await _storageService.GetVolumeAsync(executableName);
                if (savedVolume == null)
                {
                    App.Logger.LogDebug($"No saved volume found for {executableName}", "VolumeRestorationService");
                    return;
                }

                const int maxAttempts = 10;
                var attemptDelay = 500;
                bool restored = false;

                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    if (_audioSessionManager.SetSessionVolumeImmediate(executableName, savedVolume.Value))
                    {
                        _recentRestorations[executableName] = DateTime.UtcNow;
                        App.Logger.LogInfo($"Volume restored for {executableName} to {savedVolume}% (attempt {attempt + 1})",
                            "VolumeRestorationService");
                        restored = true;
                        break;
                    }

                    if (attempt >= maxAttempts - 1) break;

                    await Task.Delay(attemptDelay);
                    attemptDelay = Math.Min(attemptDelay * 2, 5000);
                }

                if (!restored)
                {
                    App.Logger.LogWarning($"Failed to restore volume for {executableName} after {maxAttempts} attempts",
                        "VolumeRestorationService");
                }
            }
            catch (Exception ex)
            {
                App.Logger.LogError($"Error restoring volume for {executableName}", ex, "VolumeRestorationService");
            }
        });
    }

    private void CleanupOldRestorations(object? state)
    {
        var cutoffTime = DateTime.UtcNow - TimeSpan.FromMinutes(5);
        var toRemove = _recentRestorations
            .Where(kvp => kvp.Value < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _recentRestorations.TryRemove(key, out _);
        }
    }

    public Task RestoreAllCurrentSessionsAsync()
    {
        return Task.Run(async () =>
        {
            try
            {
                var sessions = _audioSessionManager.GetAllSessions();
                var validSessions = sessions.Where(s => !string.IsNullOrEmpty(s.ExecutableName));
                App.Logger.LogInfo($"Restoring volumes for {sessions.Count} active sessions", "VolumeRestorationService");

                foreach (var session in validSessions)
                {
                    var savedVolume = await _storageService.GetVolumeAsync(session.ExecutableName);
                    if (savedVolume == null || Math.Abs(session.Volume - savedVolume.Value) <= 1) continue;

                    if (await _audioSessionManager.SetSessionVolume(session.ExecutableName, savedVolume.Value))
                    {
                        App.Logger.LogInfo($"Volume restored for {session.ExecutableName} from {session.Volume}% to {savedVolume}%", "VolumeRestorationService");
                    }
                    else
                    {
                        App.Logger.LogWarning($"Failed to restore volume for {session.ExecutableName}", "VolumeRestorationService");
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.LogError("Failed to restore volumes for current sessions", ex, "VolumeRestorationService");
            }
        });
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _appMonitorService.ApplicationLaunched -= OnApplicationLaunched;
        _cleanupTimer?.Dispose();
    }
}
