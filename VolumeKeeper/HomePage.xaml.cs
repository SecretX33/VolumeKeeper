using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using VolumeKeeper.Models;
using VolumeKeeper.Services;
using VolumeKeeper.Services.Managers;

namespace VolumeKeeper;

public sealed partial class HomePage : Page
{
    public ObservableCollection<ApplicationVolume> Applications { get; }
    private readonly IconService _iconService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherTimer _refreshTimer;
    private static VolumeSettingsManager VolumeSettingsManager => App.VolumeSettingsManager;

    public HomePage()
    {
        InitializeComponent();
        Applications = new ObservableCollection<ApplicationVolume>();
        _iconService = new IconService();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _refreshTimer = new DispatcherTimer();
        _refreshTimer.Interval = TimeSpan.FromSeconds(2);
        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();

        LoadAudioSessions();
        UpdateEmptyStateVisibility();
        LoadSettings();
    }

    private void LoadSettings()
    {
        AutoRestoreToggle.IsOn = VolumeSettingsManager.AutoRestoreEnabled;
    }

    private async void LoadAudioSessions()
    {
        try
        {
            var sessions = await App.AudioSessionManager.GetAllSessionsAsync();

            var sessionsWithSavedVolumes = new List<(AudioSession session, int? savedVolume)>();

            foreach (var session in sessions)
            {
                var savedVolume = VolumeSettingsManager.GetVolume(session.AppId);
                sessionsWithSavedVolumes.Add((session, savedVolume));
            }

            _dispatcherQueue.TryEnqueue(() =>
            {
                Applications.Clear();

                foreach (var (session, savedVolume) in sessionsWithSavedVolumes)
                {
                    var app = new ApplicationVolume
                    {
                        Session = session,
                        ApplicationName = Path.GetFileNameWithoutExtension(session.ExecutableName),
                        Volume = session.Volume,
                        SavedVolume = savedVolume,
                        Status = "Active",
                        LastSeen = "Just now"
                    };

                    Applications.Add(app);

                    // Load icon asynchronously
                    LoadApplicationIconAsync(app, session.IconPath);
                }

                UpdateEmptyStateVisibility();
            });
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to load audio sessions", ex, "HomePage");
        }
    }

    private void RefreshTimer_Tick(object? sender, object e)
    {
        UpdateAudioSessions();
    }

    private async void UpdateAudioSessions()
    {
        try
        {
            var sessions = await App.AudioSessionManager.GetAllSessionsAsync();

            // Get saved volumes for new sessions
            var newSessions = sessions.Where(s =>
                !Applications.Any(a => string.Equals(a.ExecutableName, s.ExecutableName, StringComparison.OrdinalIgnoreCase))).ToList();
            var newSessionsWithVolumes = new List<(AudioSession session, int? savedVolume)>();

            foreach (var session in newSessions)
            {
                var savedVolume = VolumeSettingsManager.GetVolume(session.AppId);
                newSessionsWithVolumes.Add((session, savedVolume));
            }

            _dispatcherQueue.TryEnqueue(() =>
            {
                // Update existing applications
                foreach (var app in Applications.ToList())
                {
                    var session = sessions.FirstOrDefault(s =>
                        string.Equals(s.ExecutableName, app.ExecutableName, StringComparison.OrdinalIgnoreCase));
                    if (session != null)
                    {
                        if (Math.Abs(app.Volume - session.Volume) > 1.0)
                        {
                            App.Logger.LogInfo($"Volume changed for {app.ApplicationName}: {app.Volume}% → {session.Volume}%", "HomePage");
                        }

                        app.Session = session;
                        app.Volume = session.Volume;
                        app.Status = "Active";
                        app.LastSeen = "Just now";
                    }
                    else
                    {
                        // Application no longer has audio session
                        App.Logger.LogInfo($"Audio session ended for {app.ApplicationName}", "HomePage");
                        Applications.Remove(app);
                    }
                }

                // Add new applications
                foreach (var (session, savedVolume) in newSessionsWithVolumes)
                {
                    App.Logger.LogInfo(
                        $"New audio session detected: {Path.GetFileNameWithoutExtension(session.ExecutableName)} (Volume: {session.Volume}%)",
                        "HomePage");

                    var app = new ApplicationVolume
                    {
                        Session = session,
                        ApplicationName = Path.GetFileNameWithoutExtension(session.ExecutableName),
                        Volume = session.Volume,
                        SavedVolume = savedVolume,
                        Status = "Active",
                        LastSeen = "Just now"
                    };

                    Applications.Add(app);

                    // Load icon asynchronously
                    LoadApplicationIconAsync(app, session.IconPath);
                }

                UpdateEmptyStateVisibility();
            });
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to load audio sessions", ex, "HomePage");
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        LoadAudioSessions();
    }

    private async void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = "Clear All Saved Volumes?",
                Content =
                    "This will remove all saved volume levels. Applications will no longer have their volumes automatically restored.",
                PrimaryButtonText = "Clear All",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot,
                DefaultButton = ContentDialogButton.Close
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            await VolumeSettingsManager.ClearAllConfigurationsAsync();

            Applications.Clear();
            UpdateEmptyStateVisibility();
            App.Logger.LogInfo("All saved volume levels cleared", "HomePage");
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to clear all saved volumes", ex, "HomePage");
        }
    }

    private async void AutoRestoreToggle_Toggled(object sender, RoutedEventArgs e)
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

    private async void MuteToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { CommandParameter: ApplicationVolume app }) return;

            var audioSessionService = App.AudioSessionService;

            if (app.Volume > 0)
            {
                // Store current volume before muting
                VolumeSettingsManager.SetLastVolumeBeforeMuteAndSave(app.AppId, (int)app.Volume);

                // Mute
                await audioSessionService.SetSessionVolume(app.AppId, 0);
                app.Volume = 0;
                App.Logger.LogInfo(
                    $"Muted {app.ApplicationName} (saved volume: {VolumeSettingsManager.GetLastVolumeBeforeMute(app.AppId)}%)",
                    "HomePage");
            }
            else
            {
                var savedLastVolume = VolumeSettingsManager.GetLastVolumeBeforeMute(app.AppId);
                VolumeSettingsManager.DeleteLastVolumeBeforeMuteAndSave(app.AppId);
                var lastVolume = savedLastVolume ?? 50;
                await audioSessionService.SetSessionVolume(app.ExecutableName, lastVolume);
                App.Logger.LogInfo($"Unmuted {app.ApplicationName} to {lastVolume}%", "HomePage");
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to toggle mute for application", ex, "HomePage");
        }
    }


    private void UpdateEmptyStateVisibility()
    {
        EmptyState.Visibility = Applications.Any() ? Visibility.Collapsed : Visibility.Visible;
        ApplicationListView.Visibility = Applications.Any() ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        try {
            if (sender is not Slider { Tag: ApplicationVolume app } || string.IsNullOrEmpty(app.ExecutableName)) return;

            var newVolume = (int)e.NewValue;

            // Update the audio session volume
            await App.AudioSessionService.SetSessionVolume(app.ExecutableName, newVolume);
        } catch (Exception ex)
        {
            App.Logger.LogError("Failed to change volume", ex, "HomePage");
        }
    }

    private void SaveVolume_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { CommandParameter: ApplicationVolume app }) return;

            var currentVolume = (int)app.Volume;
            VolumeSettingsManager.SetVolumeAndSave(app.AppId, currentVolume);
            app.SavedVolume = currentVolume;

            App.Logger.LogInfo($"Saved volume for {app.ApplicationName}: {currentVolume}%", "HomePage");
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to save volume", ex, "HomePage");
        }
    }

    private async void RevertVolume_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { CommandParameter: ApplicationVolume app }) return;
            if (!app.SavedVolume.HasValue) return;

            var audioSessionService = App.AudioSessionService;

            var savedVolume = app.SavedVolume.Value;
            await audioSessionService.SetSessionVolume(app.ExecutableName, savedVolume);
            app.Volume = savedVolume;

            App.Logger.LogInfo($"Reverted volume for {app.ApplicationName} to {savedVolume}%", "HomePage");
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to revert volume", ex, "HomePage");
        }
    }

    private async void LoadApplicationIconAsync(ApplicationVolume app, string? iconPath)
    {
        try
        {
            var icon = await _iconService.GetApplicationIconAsync(iconPath, app.ProcessName);
            if (icon != null)
            {
                _dispatcherQueue.TryEnqueue(() => { app.Icon = icon; });
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogWarning($"Failed to load icon for {app.ApplicationName}: {ex.Message}", "HomePage");
        }
    }

    public void Dispose()
    {
        try
        {
            _refreshTimer.Stop();
        } catch
        {
            /* Ignore exceptions during dispose */
        }
    }
}

public sealed partial class ApplicationVolume : INotifyPropertyChanged
{
    private AudioSession _session = null!; // Initialized via property
    private string _applicationName = string.Empty;
    private double _volume;
    private int? _savedVolume;
    private string _status = string.Empty;
    private string _lastSeen = string.Empty;
    private BitmapImage? _icon;
    private const double VolumeDifferenceTolerance = 0.01;

    public VolumeApplicationId AppId => _session.AppId;
    public string ProcessName => _session.ProcessName;
    public string ExecutableName => _session.ExecutableName;
    public string? ExecutablePath => _session.ExecutablePath;
    public bool IsMuted => _session.IsMuted;

    public required AudioSession Session
    {
        get => _session;
        set
        {
            _session = value;
            OnPropertyChanged(nameof(AppId));
            OnPropertyChanged(nameof(ProcessName));
            OnPropertyChanged(nameof(ExecutableName));
            OnPropertyChanged(nameof(ExecutablePath));
            OnPropertyChanged(nameof(IsMuted));
        }
    }

    public string ApplicationName
    {
        get => _applicationName;
        set
        {
            if (_applicationName == value) return;
            _applicationName = value;
            OnPropertyChanged(nameof(ApplicationName));
        }
    }


    public double Volume
    {
        get => _volume;
        set
        {
            if (!(Math.Abs(_volume - value) > VolumeDifferenceTolerance)) return;
            _volume = value;
            OnPropertyChanged(nameof(Volume));
            OnPropertyChanged(nameof(VolumeIcon));
            OnPropertyChanged(nameof(VolumeDisplayText));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(SaveButtonVisibility));
            OnPropertyChanged(nameof(RevertButtonVisibility));
            OnPropertyChanged(nameof(SavedVolumeDisplay));
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }
    }

    public string LastSeen
    {
        get => _lastSeen;
        set
        {
            if (_lastSeen != value)
            {
                _lastSeen = value;
                OnPropertyChanged(nameof(LastSeen));
            }
        }
    }


    public BitmapImage? Icon
    {
        get => _icon;
        set
        {
            if (_icon != value)
            {
                _icon = value;
                OnPropertyChanged(nameof(Icon));
            }
        }
    }


    public int? SavedVolume
    {
        get => _savedVolume;
        set
        {
            if (_savedVolume == value) return;
            _savedVolume = value;
            OnPropertyChanged(nameof(SavedVolume));
            OnPropertyChanged(nameof(SavedVolumeDisplay));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(SaveButtonVisibility));
            OnPropertyChanged(nameof(RevertButtonVisibility));
        }
    }

    public bool HasUnsavedChanges => SavedVolume.HasValue && Math.Abs(SavedVolume.Value - Volume) > 1.0;

    public string SavedVolumeDisplay => !SavedVolume.HasValue ? "No saved volume" : $"Saved: {SavedVolume}%";

    public string VolumeDisplayText => $"{(int)Volume}%";

    public Visibility SaveButtonVisibility => HasUnsavedChanges ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RevertButtonVisibility => HasUnsavedChanges ? Visibility.Visible : Visibility.Collapsed;

    public Symbol VolumeIcon =>
        Volume == 0 ? Symbol.Mute : Symbol.Volume;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
