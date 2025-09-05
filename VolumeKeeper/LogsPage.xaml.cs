using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI;
using VolumeKeeper.Services;

namespace VolumeKeeper;

public sealed partial class LogsPage : Page
{
    public ObservableCollection<Services.LogEntry> LogEntries => App.Logger.LogEntries;

    public LogsPage()
    {
        InitializeComponent();
        UpdateEmptyStateVisibility();

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
