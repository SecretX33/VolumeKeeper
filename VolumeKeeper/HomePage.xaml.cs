using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Linq;

namespace VolumeKeeper;

public sealed partial class HomePage : Page
{
    public ObservableCollection<ApplicationVolume> Applications { get; }

    public HomePage()
    {
        InitializeComponent();
        Applications = new ObservableCollection<ApplicationVolume>();
        LoadMockData();
        UpdateEmptyStateVisibility();
    }

    private void LoadMockData()
    {
        Applications.Add(new ApplicationVolume
        {
            ApplicationName = "Spotify",
            Volume = 75,
            Status = "Active",
            LastSeen = "Just now"
        });

        Applications.Add(new ApplicationVolume
        {
            ApplicationName = "Discord",
            Volume = 50,
            Status = "Active",
            LastSeen = "2 mins ago"
        });

        Applications.Add(new ApplicationVolume
        {
            ApplicationName = "Microsoft Edge",
            Volume = 100,
            Status = "Inactive",
            LastSeen = "15 mins ago"
        });

        Applications.Add(new ApplicationVolume
        {
            ApplicationName = "Steam",
            Volume = 80,
            Status = "Active",
            LastSeen = "Just now"
        });

        Applications.Add(new ApplicationVolume
        {
            ApplicationName = "Visual Studio Code",
            Volume = 0,
            Status = "Inactive",
            LastSeen = "1 hour ago"
        });
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var app in Applications)
        {
            app.LastSeen = "Just now";
        }
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
}

public class ApplicationVolume : System.ComponentModel.INotifyPropertyChanged
{
    private string applicationName = string.Empty;
    private double volume;
    private string status = string.Empty;
    private string lastSeen = string.Empty;

    public string ApplicationName
    {
        get => applicationName;
        set
        {
            if (applicationName != value)
            {
                applicationName = value;
                OnPropertyChanged(nameof(ApplicationName));
            }
        }
    }

    public double Volume
    {
        get => volume;
        set
        {
            if (volume != value)
            {
                volume = value;
                OnPropertyChanged(nameof(Volume));
            }
        }
    }

    public string Status
    {
        get => status;
        set
        {
            if (status != value)
            {
                status = value;
                OnPropertyChanged(nameof(Status));
            }
        }
    }

    public string LastSeen
    {
        get => lastSeen;
        set
        {
            if (lastSeen != value)
            {
                lastSeen = value;
                OnPropertyChanged(nameof(LastSeen));
            }
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
