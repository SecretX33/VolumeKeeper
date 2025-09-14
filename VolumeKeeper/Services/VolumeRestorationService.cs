using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VolumeKeeper.Services.Managers;
using VolumeKeeper.Util;

namespace VolumeKeeper.Services;

public partial class VolumeRestorationService : IDisposable
{
    private readonly AudioSessionService _audioSessionService;
    private readonly AudioSessionManager _sessionManager;
    private readonly VolumeSettingsManager _settingsManager;
    private readonly ApplicationMonitorService _appMonitorService;
    private readonly ConcurrentDictionary<string, DateTime> _recentRestorations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _restorationCooldown = TimeSpan.FromSeconds(1);
    private readonly AtomicReference<bool> _isDisposed = new(false);

    public VolumeRestorationService(
        AudioSessionService audioSessionService,
        AudioSessionManager sessionManager,
        VolumeSettingsManager settingsManager,
        ApplicationMonitorService appMonitorService)
    {
        _audioSessionService = audioSessionService;
        _sessionManager = sessionManager;
        _settingsManager = settingsManager;
        _appMonitorService = appMonitorService;
        _appMonitorService.ApplicationLaunched += OnApplicationLaunched;
        _cleanupTimer = new Timer(CleanupOldRestorations, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    private async void OnApplicationLaunched(object? sender, ApplicationLaunchEventArgs e)
    {
        try {
            if (string.IsNullOrEmpty(e.ExecutableName))
                return;

            var settings = await _settingsManager.GetSettingsAsync().ConfigureAwait(false);
            if (!settings.AutoRestoreEnabled)
            {
                App.Logger.LogDebug($"Auto-restore disabled, skipping volume restoration for {e.ExecutableName}", "VolumeRestorationService");
                return;
            }

            if (_recentRestorations.TryGetValue(e.ExecutableName, out var lastRestoration))
            {
                if (DateTime.UtcNow - lastRestoration < _restorationCooldown)
                {
                    App.Logger.LogInfo($"Skipping volume restoration for {e.ExecutableName} (cooldown active)", "VolumeRestorationService");
                    return;
                }
            }

            await Task.Run(() => RestoreVolumeAsync(e.ExecutableName));
        } catch (Exception ex)
        {
            App.Logger.LogError($"Error handling application launch for {e.ExecutableName}", ex, "VolumeRestorationService");
        }
    }

    private async Task RestoreVolumeAsync(string executableName)
    {
        try
        {
            var savedVolume = await _settingsManager.GetVolumeAsync(executableName);
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
                if (_audioSessionService.SetSessionVolumeImmediate(executableName, savedVolume.Value))
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

    public async Task RestoreAllCurrentSessionsAsync()
    {
        try
        {
            var sessions = await _sessionManager.GetAllSessionsAsync();
            var validSessions = sessions.Where(s => !string.IsNullOrEmpty(s.ExecutableName));
            App.Logger.LogInfo($"Restoring volumes for {sessions.Count} active sessions", "VolumeRestorationService");

            foreach (var session in validSessions)
            {
                var savedVolume = _settingsManager.GetVolume(session.VolumeId);
                if (savedVolume == null || Math.Abs(session.Volume - savedVolume.Value) <= 1) continue;

                if (await _audioSessionService.SetSessionVolume(session.ExecutableName, savedVolume.Value))
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
    }

    public void Dispose()
    {
        if (!_isDisposed.CompareAndSet(false, true))
            return;

        _appMonitorService.ApplicationLaunched -= OnApplicationLaunched;
        _cleanupTimer.Dispose();
    }
}
