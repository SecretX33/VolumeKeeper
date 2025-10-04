using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VolumeKeeper.Controls;
using VolumeKeeper.Models.Log;
using VolumeKeeper.Services.Managers;

namespace VolumeKeeper;

public sealed partial class LogsPage : Page
{
    public ObservableCollection<LogEntry> LogEntries => App.Logger.LogEntries;
    private static VolumeSettingsManager VolumeSettingsManager => App.VolumeSettingsManager;

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

    private void LoadSettings()
    {
        AutoScrollToggle.IsOn = VolumeSettingsManager.AutoScrollLogsEnabled;
    }

    private void AutoScrollToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not CompactToggleSwitch toggle) return;
        VolumeSettingsManager.SetAutoScrollLogsEnabledAndSave(toggle.IsOn);
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
