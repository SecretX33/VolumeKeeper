using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using VolumeKeeper.Models;
using VolumeKeeper.Services;
using VolumeKeeper.Services.Managers;

namespace VolumeKeeper;

public sealed partial class HomePage : Page, IDisposable
{
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
            if (sender is not ToggleSwitch toggle) return;
            VolumeSettingsManager.SetAutoRestoreEnabledAndSave(toggle.IsOn);
            App.Logger.LogInfo($"Auto-restore toggled to {(toggle.IsOn ? "enabled" : "disabled")}", "HomePage");
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to update auto-restore setting", ex, "HomePage");
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
                App.Logger.LogInfo(
                    $"Muted {app.ProcessDisplayName} (saved volume: {VolumeSettingsManager.GetLastVolumeBeforeMute(app.AppId)}%)",
                    "HomePage");
            }
            else
            {
                var lastVolume = VolumeSettingsManager.GetLastVolumeBeforeMute(app.AppId) ?? 50;
                VolumeSettingsManager.DeleteLastVolumeBeforeMuteAndSave(app.AppId);

                _ = audioSessionService.SetMuteSessionImmediateAsync(app.AppId, false);
                _ = audioSessionService.SetSessionVolumeImmediate(app.AppId, lastVolume);
                App.Logger.LogInfo($"Unmuted {app.ProcessDisplayName} to {lastVolume}%", "HomePage");
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to toggle mute for application", ex, "HomePage");
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
            App.Logger.LogError("Failed to change volume", ex, "HomePage");
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

                App.Logger.LogInfo($"Unpinned volume for {app.ExecutableName} (PID: {app.ProcessId})", "HomePage");
            }
            else
            {
                // Pin or re-pin: Save the current volume
                VolumeSettingsManager.SetVolumeAndSave(app.AppId, currentVolume);
                app.PinnedVolume = currentVolume;

                App.Logger.LogInfo($"Pinned volume for {app.ExecutableName} (PID: {app.ProcessId}): {currentVolume}%", "HomePage");
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to pin/unpin volume", ex, "HomePage");
        }
    }

    private async void RevertVolume_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { CommandParameter: ObservableAudioSession app }) return;
            if (!app.PinnedVolume.HasValue) return;

            var savedVolume = app.PinnedVolume.Value;

            await AudioSessionService.SetSessionVolumeAsync(app.AppId, savedVolume);

            App.Logger.LogInfo($"Reverted volume for {app.ProcessDisplayName} to {savedVolume}%", "HomePage");
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to revert volume", ex, "HomePage");
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
