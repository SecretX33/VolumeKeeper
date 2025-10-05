using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using VolumeKeeper.Controls;
using VolumeKeeper.Models;
using VolumeKeeper.Services;
using VolumeKeeper.Services.Log;
using VolumeKeeper.Services.Managers;

namespace VolumeKeeper;

public sealed partial class HomePage : Page, IDisposable
{
    private readonly LoggingService _logger = App.Logger.Named();
    private static VolumeSettingsManager VolumeSettingsManager => App.VolumeSettingsManager;
    private static AudioSessionManager AudioSessionManager => App.AudioSessionManager;
    private static AudioSessionService AudioSessionService => App.AudioSessionService;
    private ObservableCollection<ObservableAudioSession> Applications => AudioSessionManager.AudioSessions;
    private readonly NotifyCollectionChangedEventHandler? _applicationsOnCollectionChanged;

    public HomePage()
    {
        InitializeComponent();
        UpdateEmptyStateVisibility();
        _applicationsOnCollectionChanged = (_, _) => UpdateEmptyStateVisibility();
        Applications.CollectionChanged += _applicationsOnCollectionChanged;
        LoadSettings();
    }

    private void LoadSettings()
    {
        AutoRestoreToggle.IsOn = VolumeSettingsManager.AutoRestoreEnabled;
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _ = Task.Run(AudioSessionManager.UpdateAllSessions);
    }

    private void AutoRestoreToggle_Toggled(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not CompactToggleSwitch toggle) return;
            VolumeSettingsManager.SetAutoRestoreEnabledAndSave(toggle.IsOn);
            _logger.Debug($"Auto-restore toggled to {(toggle.IsOn ? "enabled" : "disabled")}");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to update auto-restore setting", ex);
        }
    }

    private void MuteToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { CommandParameter: ObservableAudioSession app }) return;

            var audioSessionService = App.AudioSessionService;

            if (app is { IsMuted: false, Volume: > 0 })
            {
                // Store current volume before muting
                VolumeSettingsManager.SetLastVolumeBeforeMuteAndSave(app.AppId, app.Volume);

                // Mute
                _ = audioSessionService.SetMuteSessionImmediateAsync(app.AppId, true);
                _logger.Info(
                    $"Muted {app.ExecutableName} (PID: {app.ProcessId}) (saved volume: {VolumeSettingsManager.GetLastVolumeBeforeMute(app.AppId)}%)");
            }
            else
            {
                var lastVolume = VolumeSettingsManager.GetLastVolumeBeforeMute(app.AppId) ?? 50;
                VolumeSettingsManager.DeleteLastVolumeBeforeMuteAndSave(app.AppId);

                _ = audioSessionService.SetMuteSessionImmediateAsync(app.AppId, false);
                _ = audioSessionService.SetSessionVolumeImmediate(app.AppId, lastVolume);
                _logger.Info($"Unmuted {app.ExecutableName} (PID: {app.ProcessId}) to {lastVolume}%");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to toggle mute for application", ex);
        }
    }

    private void UpdateEmptyStateVisibility()
    {
        var hasAnyItems = Applications.Any();
        EmptyState.Visibility = hasAnyItems ? Visibility.Collapsed : Visibility.Visible;
        ApplicationListView.Visibility = hasAnyItems ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        try {
            if (sender is not Slider { Tag: ObservableAudioSession app } || string.IsNullOrEmpty(app.ExecutableName)) return;

            var newVolume = (int)e.NewValue;

            // Update the audio session volume
            await App.AudioSessionService.SetSessionVolumeAsync(app.AppId, newVolume);
        } catch (Exception ex)
        {
            _logger.Error("Failed to change volume", ex);
        }
    }

    private void PinVolume_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { CommandParameter: ObservableAudioSession app }) return;

            var currentVolume = app.Volume;

            if (app.PinnedVolume.HasValue && app.PinnedVolume.Value == currentVolume)
            {
                // Unpin: Remove the pinned volume (only if current equals pinned)
                VolumeSettingsManager.DeleteVolumeAndSave(app.AppId);
                app.PinnedVolume = null;

                _logger.Info($"Unpinned volume for {app.ExecutableName} (PID: {app.ProcessId})");
            }
            else
            {
                // Pin or re-pin: Save the current volume
                VolumeSettingsManager.SetVolumeAndSave(app.AppId, currentVolume);
                app.PinnedVolume = currentVolume;

                _logger.Info($"Pinned volume for {app.ExecutableName} (PID: {app.ProcessId}): {currentVolume}%");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to pin/unpin volume", ex);
        }
    }

    private void RevertVolume_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { CommandParameter: ObservableAudioSession app }) return;
            if (!app.PinnedVolume.HasValue) return;

            var savedVolume = app.PinnedVolume.Value;

            AudioSessionService.SetSessionVolumeImmediate(app.AppId, savedVolume);

            _logger.Info($"Reverted volume for {app.ExecutableName} (PID: {app.ProcessId}) to {savedVolume}%");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to revert volume", ex);
        }
    }

    public void Dispose()
    {
        try
        {
            Applications.CollectionChanged -= _applicationsOnCollectionChanged;
        } catch
        {
            /* Ignore errors on dispose */
        }
    }
}
