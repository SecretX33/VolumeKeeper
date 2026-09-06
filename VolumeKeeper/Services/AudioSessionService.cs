using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using VolumeKeeper.Models;
using VolumeKeeper.Services.Log;
using VolumeKeeper.Util;
using AudioSessionManager = VolumeKeeper.Services.Managers.AudioSessionManager;

namespace VolumeKeeper.Services;

public sealed class AudioSessionService(
    AudioSessionManager sessionManager,
    DispatcherQueue mainThreadQueue
)
{
    private readonly Logger _logger = App.Logger.Named();

    public Task<bool> SetSessionVolumeImmediate(VolumeApplicationId volumeApplicationId, int volumePercentage)
    {
        return mainThreadQueue.TryFetchImmediate(async () => {
            var session = sessionManager.GetSessionById(volumeApplicationId);
            var attempts = 0;

            while (session == null && attempts++ < 12)
            {
                _logger.Debug($"Audio session of {volumeApplicationId} not found, retrying... ({attempts}x)");
                await Task.Delay(25).ConfigureAwait(false);
                session = sessionManager.GetSessionById(volumeApplicationId);
            }

            if (session == null)
            {
                _logger.Warn($"No audio session found for {volumeApplicationId}, could not set volume to {volumePercentage} after {attempts} tries");
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
        });
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

    public async void RestorePinnedVolumeOfAllOpenedApps()
    {
        RequireMainThreadAccess();

        var audioSessions = sessionManager.AudioSessions.ToList();
        var tasks = audioSessions.Where(app => app.PinnedVolume.HasValue && app.Volume != app.PinnedVolume.Value)
            .Select(app =>
            {
                var pinnedVolume = app.PinnedVolume!.Value;
                return Task.Run(async () =>
                {
                    var result = await SetSessionVolumeImmediate(app.AppId, pinnedVolume);
                    if (result) _logger.Info($"Restored volume for {app.ExecutableName} (PID: {app.ProcessId}) to pinned volume {pinnedVolume}");
                    return result;
                });
            });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var restoredCount = results.Count(result => result);

        if (restoredCount > 0)
        {
            _logger.Info($"Auto-restore enabled: Restored {restoredCount} application(s) to their pinned volumes");
        }
    }

    private void RequireMainThreadAccess([CallerMemberName] string caller = "")
    {
        if (!mainThreadQueue.HasThreadAccess)
            throw new InvalidOperationException($"{caller} must be called on the UI thread");
    }
}
