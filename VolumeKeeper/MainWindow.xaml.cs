using Windows.Graphics;
using H.NotifyIcon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VolumeKeeper.Models;

namespace VolumeKeeper;

public sealed partial class MainWindow : Window
{
    private const WindowId WindowId = Models.WindowId.Main;

    public MainWindow()
    {
        InitializeComponent();
        Title = "VolumeKeeper";
        ExtendsContentIntoTitleBar = true;
        LoadWindowSettings();
        NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems[0];
        Closed += MainWindow_Closed;
        NavigateToPage("Home");
        SizeChanged += MainWindow_SizeChanged;
    }

    private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is not NavigationViewItem item) return;

        var tag = item.Tag?.ToString();
        if (!string.IsNullOrEmpty(tag))
        {
            NavigateToPage(tag);
        }
    }

    private void NavigateToPage(string tag)
    {
        var pageType = tag switch
        {
            "Home" => typeof(HomePage),
            "Logs" => typeof(LogsPage),
            _ => null
        };

        if (pageType != null)
        {
            ContentFrame.Navigate(pageType);
        }
    }

    private void LoadWindowSettings()
    {
        var appWindow = AppWindow;
        if (appWindow == null) return;

        var windowSettings = App.WindowSettingsManager.Get(WindowId);

        // Apply maximize state if needed
        var presenter = appWindow.Presenter as OverlappedPresenter;
        if (presenter != null)
        {
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }

        var size = new SizeInt32
        {
            Width = (int)windowSettings.Width,
            Height = (int)windowSettings.Height
        };
        appWindow.Resize(size);

        // Set window position if we have saved values, otherwise center on screen
        if (windowSettings is { X: not null, Y: not null })
        {
            var position = new PointInt32
            {
                X = (int)windowSettings.X,
                Y = (int)windowSettings.Y
            };
            appWindow.Move(position);
        }
        else
        {
            // Center the window on the primary display
            CenterWindowOnScreen(appWindow);
        }

        if (presenter != null && windowSettings.IsMaximized)
        {
            presenter.Maximize();
        }
    }

    private void CenterWindowOnScreen(AppWindow appWindow)
    {
        // Get the primary display work area
        var displayArea = DisplayArea.Primary;
        if (displayArea == null) return;

        var workArea = displayArea.WorkArea;
        var windowSize = appWindow.Size;
        var centeredPosition = new PointInt32
        {
            X = (workArea.Width - windowSize.Width) / 2 + workArea.X,
            Y = (workArea.Height - windowSize.Height) / 2 + workArea.Y
        };

        appWindow.Move(centeredPosition);
    }

    private void SaveWindowSettings(bool saveImmediately = false)
    {
        // Get current window state using AppWindow
        var appWindow = AppWindow;
        if (appWindow == null) return;

        var windowSettings = App.WindowSettingsManager.Get(WindowId);

        // Check if window is maximized
        var presenter = appWindow.Presenter as OverlappedPresenter;
        var isMaximized = presenter?.State == OverlappedPresenterState.Maximized;

        // Only save size and position if not maximized
        var newWindowSettings = new WindowSettings(
            X: !isMaximized ? appWindow.Position.X : windowSettings.X,
            Y: !isMaximized ? appWindow.Position.Y : windowSettings.Y,
            Width: !isMaximized ? appWindow.ClientSize.Width : windowSettings.Width,
            Height: !isMaximized ? appWindow.ClientSize.Height : windowSettings.Height,
            IsMaximized: isMaximized
        );
        if (windowSettings == newWindowSettings) return;

        App.WindowSettingsManager.SetAndSave(WindowId, newWindowSettings, saveImmediately);
    }

    private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        SaveWindowSettings();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        args.Handled = true;
        this.Hide();
        SaveWindowSettings(true);
    }
}
