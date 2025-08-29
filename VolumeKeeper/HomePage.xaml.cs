using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using VolumeKeeper.Services;
using Microsoft.UI.Dispatching;

namespace VolumeKeeper;

public sealed partial class HomePage : Page
{
    public ObservableCollection<ApplicationVolume> Applications { get; }
    private readonly AudioSessionService _audioService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherTimer _refreshTimer;

    public HomePage()
    {
        InitializeComponent();
        Applications = new ObservableCollection<ApplicationVolume>();
        _audioService = new AudioSessionService();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _refreshTimer = new DispatcherTimer();
        _refreshTimer.Interval = TimeSpan.FromSeconds(2);
        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();

        LoadAudioSessions();
        UpdateEmptyStateVisibility();
    }

    private void LoadAudioSessions()
    {
        try
        {
            var sessions = _audioService.GetActiveAudioSessions();

            _dispatcherQueue.TryEnqueue(() =>
            {
                Applications.Clear();

                foreach (var session in sessions)
                {
                    Applications.Add(new ApplicationVolume
                    {
                        ApplicationName = session.ApplicationName,
                        ProcessName = session.ProcessName,
                        Volume = session.Volume,
                        Status = session.IsActive ? "Active" : "Inactive",
                        LastSeen = "Just now",
                        IsMuted = session.IsMuted
                    });
                }

                UpdateEmptyStateVisibility();
            });
        }
        catch (Exception)
        {
            // Handle silently for now
        }
    }

    private void RefreshTimer_Tick(object? sender, object e)
    {
        UpdateAudioSessions();
    }

    private void UpdateAudioSessions()
    {
        try
        {
            var sessions = _audioService.GetActiveAudioSessions();

            _dispatcherQueue.TryEnqueue(() =>
            {
                // Update existing applications
                foreach (var app in Applications.ToList())
                {
                    var session = sessions.FirstOrDefault(s => s.ProcessName == app.ProcessName);
                    if (session != null)
                    {
                        app.Volume = session.Volume;
                        app.Status = session.IsActive ? "Active" : "Inactive";
                        app.IsMuted = session.IsMuted;
                        app.LastSeen = "Just now";
                    }
                    else
                    {
                        // Application no longer has audio session
                        Applications.Remove(app);
                    }
                }

                // Add new applications
                foreach (var session in sessions)
                {
                    if (!Applications.Any(a => a.ProcessName == session.ProcessName))
                    {
                        Applications.Add(new ApplicationVolume
                        {
                            ApplicationName = session.ApplicationName,
                            ProcessName = session.ProcessName,
                            Volume = session.Volume,
                            Status = session.IsActive ? "Active" : "Inactive",
                            LastSeen = "Just now",
                            IsMuted = session.IsMuted
                        });
                    }
                }

                UpdateEmptyStateVisibility();
            });
        }
        catch (Exception)
        {
            // Handle silently for now
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

    public void Dispose()
    {
        _refreshTimer?.Stop();
        _audioService?.Dispose();
    }
}

public class ApplicationVolume : System.ComponentModel.INotifyPropertyChanged
{
    private string _applicationName = string.Empty;
    private string _processName = string.Empty;
    private double _volume;
    private string _status = string.Empty;
    private string _lastSeen = string.Empty;
    private bool _isMuted;
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

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
