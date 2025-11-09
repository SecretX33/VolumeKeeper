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
    private readonly Logger _logger = App.Logger.Named();
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
        AudioSessionManager.ScheduleRefreshDeviceAndAudioSessions(immediately: true);
    }

    private void AutoRestoreToggle_Toggled(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not CompactToggleSwitch toggle) return;
            VolumeSettingsManager.SetAutoRestoreEnabledAndSave(toggle.IsOn);
            _logger.Debug($"Auto-restore toggled to {(toggle.IsOn ? "enabled" : "disabled")}");

            // When auto-restore is re-enabled, restore all pinned volumes for currently open apps
            if (toggle.IsOn)
            {
                AudioSessionService.RestorePinnedVolumeOfAllOpenedApps();
            }
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

            if (!app.IsMuted)
            {
                // Mute
                audioSessionService.SetMuteSessionImmediate(app.AppId, mute: true);
            }
            else
            {
                // Unmute
                audioSessionService.SetMuteSessionImmediate(app.AppId, mute: false);
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
            if (app.Volume == newVolume) return;

            // If the app has a pinned volume, instantly set the new volume as the new pinned volume to update its value on the UI immediately
            if (app.PinnedVolume.HasValue)
            {
                VolumeSettingsManager.SetVolumeAndSave(app.AppId, newVolume);
                app.PinnedVolume = newVolume;
            }

            // Finally, update the audio session volume
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

                _logger.Info($"Pinned volume for {app.ExecutableName} (PID: {app.ProcessId}): {currentVolume}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to pin/unpin volume", ex);
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
