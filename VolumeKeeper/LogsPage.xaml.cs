using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VolumeKeeper.Models.Log;

namespace VolumeKeeper;

public sealed partial class LogsPage : Page
{
    public ObservableCollection<LogEntry> LogEntries => App.Logger.LogEntries;

    public LogsPage()
    {
        InitializeComponent();
        UpdateEmptyStateVisibility();
        LoadSettings();

        // Subscribe to collection changes
        LogEntries.CollectionChanged += (s, e) =>
        {
            UpdateEmptyStateVisibility();

            if (AutoScrollToggle.IsOn && LogEntries.Count > 0)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    // Scroll to top (latest entry)
                    LogScrollViewer.ChangeView(null, 0, null);
                });
            }
        };
    }

    private async void LoadSettings()
    {
        if (App.VolumeStorageService != null)
        {
            var settings = await App.VolumeStorageService.GetSettingsAsync();
            AutoScrollToggle.IsOn = settings.AutoScrollLogsEnabled;
        }
    }

    private async void AutoScrollToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (App.VolumeStorageService != null && sender is ToggleSwitch toggle)
        {
            var settings = await App.VolumeStorageService.GetSettingsAsync();
            settings.AutoScrollLogsEnabled = toggle.IsOn;
            await App.VolumeStorageService.SaveSettingsAsync(settings);
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
