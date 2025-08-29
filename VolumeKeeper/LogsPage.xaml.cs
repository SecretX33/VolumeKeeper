using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI;

namespace VolumeKeeper;

public sealed partial class LogsPage : Page
{
    public ObservableCollection<LogEntry> LogEntries { get; }

    public LogsPage()
    {
        InitializeComponent();
        LogEntries = new ObservableCollection<LogEntry>();
        LoadMockLogs();
        UpdateEmptyStateVisibility();
    }

    private void LoadMockLogs()
    {
        AddLog(LogType.Info, "VolumeKeeper started", DateTime.Now.AddMinutes(-30));
        AddLog(LogType.ApplicationDetected, "Detected application: Spotify", DateTime.Now.AddMinutes(-28));
        AddLog(LogType.VolumeChanged, "Spotify volume changed to 75%", DateTime.Now.AddMinutes(-25));
        AddLog(LogType.ApplicationDetected, "Detected application: Discord", DateTime.Now.AddMinutes(-20));
        AddLog(LogType.VolumeChanged, "Discord volume changed to 50%", DateTime.Now.AddMinutes(-18));
        AddLog(LogType.ApplicationDetected, "Detected application: Microsoft Edge", DateTime.Now.AddMinutes(-15));
        AddLog(LogType.VolumeRestored, "Restored Microsoft Edge volume to 100%", DateTime.Now.AddMinutes(-15));
        AddLog(LogType.ApplicationDetected, "Detected application: Steam", DateTime.Now.AddMinutes(-10));
        AddLog(LogType.VolumeChanged, "Steam volume changed to 80%", DateTime.Now.AddMinutes(-8));
        AddLog(LogType.ApplicationClosed, "Application closed: Visual Studio Code", DateTime.Now.AddMinutes(-5));
        AddLog(LogType.Info, "Auto-save triggered", DateTime.Now.AddMinutes(-2));
        AddLog(LogType.VolumeChanged, "Spotify volume changed to 65%", DateTime.Now.AddMinutes(-1));
    }

    private void AddLog(LogType type, string message, DateTime? timestamp = null)
    {
        var entry = new LogEntry
        {
            Type = type,
            Message = message,
            Timestamp = (timestamp ?? DateTime.Now).ToString("HH:mm:ss")
        };

        LogEntries.Insert(0, entry);

        if (AutoScrollToggle.IsOn && LogEntries.Count > 1)
        {
            DispatcherQueue.TryEnqueue(() => LogScrollViewer.ChangeView(null, 0, null));
        }
    }

    private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
    {
        LogEntries.Clear();
        UpdateEmptyStateVisibility();
    }

    private void UpdateEmptyStateVisibility()
    {
        EmptyState.Visibility = LogEntries.Any() ? Visibility.Collapsed : Visibility.Visible;
        LogScrollViewer.Visibility = LogEntries.Any() ? Visibility.Visible : Visibility.Collapsed;
    }
}

public class LogEntry
{
    public LogType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;

    public Symbol IconSymbol => Type switch
    {
        LogType.Info => Symbol.Important,
        LogType.ApplicationDetected => Symbol.Audio,
        LogType.ApplicationClosed => Symbol.Cancel,
        LogType.VolumeChanged => Symbol.Volume,
        LogType.VolumeRestored => Symbol.Repair,
        LogType.Error => Symbol.ReportHacked,
        _ => Symbol.Important
    };

    public Brush TypeColor => GetColorForType(Type);

    public Brush GetColorForType(LogType type)
    {
        var color = type switch
        {
            LogType.Info => Color.FromArgb(255, 0, 120, 212),
            LogType.ApplicationDetected => Color.FromArgb(255, 0, 204, 106),
            LogType.ApplicationClosed => Color.FromArgb(255, 255, 185, 0),
            LogType.VolumeChanged => Color.FromArgb(255, 136, 23, 152),
            LogType.VolumeRestored => Color.FromArgb(255, 0, 178, 148),
            LogType.Error => Color.FromArgb(255, 232, 17, 35),
            _ => Color.FromArgb(255, 118, 118, 118)
        };
        return new SolidColorBrush(color);
    }
}

public enum LogType
{
    Info,
    ApplicationDetected,
    ApplicationClosed,
    VolumeChanged,
    VolumeRestored,
    Error
}
