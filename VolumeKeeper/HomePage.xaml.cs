using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using VolumeKeeper.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;

namespace VolumeKeeper;

public sealed partial class HomePage : Page
{
    public ObservableCollection<ApplicationVolume> Applications { get; }
    private readonly IconService _iconService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherTimer _refreshTimer;

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
    }

    private async void LoadAudioSessions()
    {
        try
        {
            if (App.AudioSessionManager == null) return;
            var sessions = App.AudioSessionManager.GetAllSessions();

            var sessionsWithSavedVolumes = new List<(Services.AudioSession session, int? savedVolume)>();

            foreach (var session in sessions)
            {
                var savedVolume = App.VolumeStorageService != null
                    ? await App.VolumeStorageService.GetVolumeAsync(session.ExecutableName)
                    : null;
                sessionsWithSavedVolumes.Add((session, savedVolume));
            }

            _dispatcherQueue.TryEnqueue(() =>
            {
                Applications.Clear();

                foreach (var (session, savedVolume) in sessionsWithSavedVolumes)
                {

                    var app = new ApplicationVolume
                    {
                        ApplicationName = Path.GetFileNameWithoutExtension(session.ExecutableName),
                        ProcessName = session.ProcessName,
                        ExecutableName = session.ExecutableName,
                        Volume = session.Volume,
                        SavedVolume = savedVolume,
                        Status = "Active",
                        LastSeen = "Just now",
                        IsMuted = session.IsMuted
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
            if (App.AudioSessionManager == null) return;
            var sessions = App.AudioSessionManager.GetAllSessions();

            // Get saved volumes for new sessions
            var newSessions = sessions.Where(s => !Applications.Any(a => string.Equals(a.ExecutableName, s.ExecutableName, StringComparison.OrdinalIgnoreCase))).ToList();
            var newSessionsWithVolumes = new List<(Services.AudioSession session, int? savedVolume)>();

            foreach (var session in newSessions)
            {
                var savedVolume = App.VolumeStorageService != null
                    ? await App.VolumeStorageService.GetVolumeAsync(session.ExecutableName)
                    : null;
                newSessionsWithVolumes.Add((session, savedVolume));
            }

            _dispatcherQueue.TryEnqueue(() =>
            {
                // Update existing applications
                foreach (var app in Applications.ToList())
                {
                    var session = sessions.FirstOrDefault(s => string.Equals(s.ExecutableName, app.ExecutableName, StringComparison.OrdinalIgnoreCase));
                    if (session != null)
                    {
                        if (Math.Abs(app.Volume - session.Volume) > 1.0)
                        {
                            App.Logger.LogInfo($"Volume changed for {app.ApplicationName}: {app.Volume}% → {session.Volume}%", "HomePage");
                        }

                        app.Volume = session.Volume;
                        app.Status = "Active";
                        app.IsMuted = session.IsMuted;
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
                    App.Logger.LogInfo($"New audio session detected: {Path.GetFileNameWithoutExtension(session.ExecutableName)} (Volume: {session.Volume}%)", "HomePage");

                    var app = new ApplicationVolume
                    {
                        ApplicationName = Path.GetFileNameWithoutExtension(session.ExecutableName),
                        ProcessName = session.ProcessName,
                        ExecutableName = session.ExecutableName,
                        Volume = session.Volume,
                        SavedVolume = savedVolume,
                        Status = "Active",
                        LastSeen = "Just now",
                        IsMuted = session.IsMuted
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

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        Applications.Clear();
        UpdateEmptyStateVisibility();
    }

    private void RemoveApplication_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.CommandParameter is ApplicationVolume app)
        {
            Applications.Remove(app);
            UpdateEmptyStateVisibility();
        }
    }

    private void UpdateEmptyStateVisibility()
    {
        EmptyState.Visibility = Applications.Any() ? Visibility.Collapsed : Visibility.Visible;
        ApplicationListView.Visibility = Applications.Any() ? Visibility.Visible : Visibility.Collapsed;
    }

    private void VolumeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (sender is not Slider { Tag: ApplicationVolume app } || string.IsNullOrEmpty(app.ExecutableName)) return;

        var audioSessionManager = App.AudioSessionManager;
        if (audioSessionManager == null)
        {
            App.Logger.LogError("Audio session manager not initialized, cannot save volume of: " + app.ApplicationName);
            return;
        }

        var newVolume = (int)e.NewValue;
        _ = audioSessionManager.SetSessionVolume(app.ExecutableName, newVolume);
    }

    private async void LoadApplicationIconAsync(ApplicationVolume app, string? iconPath)
    {
        try
        {
            var icon = await _iconService.GetApplicationIconAsync(iconPath, app.ProcessName);
            if (icon != null)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    app.Icon = icon;
                });
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogWarning($"Failed to load icon for {app.ApplicationName}: {ex.Message}", "HomePage");
        }
    }

    public void Dispose()
    {
        _refreshTimer?.Stop();
    }
}

public class ApplicationVolume : System.ComponentModel.INotifyPropertyChanged
{
    private string _applicationName = string.Empty;
    private string _processName = string.Empty;
    private string _executableName = string.Empty;
    private double _volume;
    private int? _savedVolume;
    private string _status = string.Empty;
    private string _lastSeen = string.Empty;
    private bool _isMuted;
    private BitmapImage? _icon;
    private const double VolumeDifferenceTolerance = 0.01;

    public string ApplicationName
    {
        get => _applicationName;
        set
        {
            if (_applicationName != value)
            {
                _applicationName = value;
                OnPropertyChanged(nameof(ApplicationName));
            }
        }
    }

    public string ProcessName
    {
        get => _processName;
        set
        {
            if (_processName != value)
            {
                _processName = value;
                OnPropertyChanged(nameof(ProcessName));
            }
        }
    }

    public double Volume
    {
        get => _volume;
        set
        {
            if (Math.Abs(_volume - value) > VolumeDifferenceTolerance)
            {
                _volume = value;
                OnPropertyChanged(nameof(Volume));
            }
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

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (_isMuted != value)
            {
                _isMuted = value;
                OnPropertyChanged(nameof(IsMuted));
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

    public string ExecutableName
    {
        get => _executableName;
        set
        {
            if (_executableName != value)
            {
                _executableName = value;
                OnPropertyChanged(nameof(ExecutableName));
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
        }
    }

    public string SavedVolumeDisplay => SavedVolume.HasValue ? $"Saved: {SavedVolume}%" : "";

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
