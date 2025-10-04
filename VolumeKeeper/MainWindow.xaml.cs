using System;
using Windows.Graphics;
using H.NotifyIcon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VolumeKeeper.Models;
using VolumeKeeper.Util;

namespace VolumeKeeper;

public sealed partial class MainWindow : Window
{
    private const WindowId WindowId = Models.WindowId.Main;
    private readonly PointInt32 _minWindowSize = new(400, 350);
    private Win32WindowHelper? _helper; // Keep a reference to prevent garbage collection

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
        Activated += MainWindow_Activated;
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

        _helper = new Win32WindowHelper(this);
        _helper.SetWindowMinMaxSize(minWindowSize: _minWindowSize);

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
            Width = (int)Math.Max(windowSettings.Width, _minWindowSize.X),
            Height = (int)Math.Max(windowSettings.Height, _minWindowSize.Y)
        };
        appWindow.Resize(size);

        // Set window position if we have saved values, otherwise center on screen
        if (windowSettings is { X: not null, Y: not null })
        {
            var position = new PointInt32
            {
                X = windowSettings.X.Value,
                Y = windowSettings.Y.Value
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
        if (displayArea == null)
        {
            App.Logger.LogWarning("Failed to get primary display area, thus couldn't center window on screen", "MainWindow");
            return;
        }

        var workArea = displayArea.WorkArea;
        var windowSize = appWindow.Size;
        var centeredPosition = new PointInt32
        {
            X = (workArea.Width - windowSize.Width) / 2 + workArea.X,
            Y = (workArea.Height - windowSize.Height) / 2 + workArea.Y
        };

        appWindow.Move(centeredPosition);
    }

    private bool IsWindowOutOfBounds(AppWindow appWindow)
    {
        // Get the primary display work area
        var displayArea = DisplayArea.Primary;
        if (displayArea == null)
        {
            App.Logger.LogWarning("Failed to get primary display area, thus couldn't determine if window is out of bounds", "MainWindow");
            return false;
        }

        var workArea = displayArea.WorkArea;
        var windowPosition = appWindow.Position;
        var windowSize = appWindow.Size;

        var workAreaStartBounds = new PointInt32(workArea.X, workArea.X);
        var workAreaEndBounds = new PointInt32(workArea.X + workArea.Width, workArea.Y + workArea.Height);

        const double maximumAllowedOutOfBoundsAmount = 0.75;
        var outOnTopLeftOfTheScreen = windowPosition.X < workAreaStartBounds.X
            || windowPosition.Y < workAreaStartBounds.Y;
        var outOnBottomRightOfTheScreen = windowPosition.X + windowSize.Width * maximumAllowedOutOfBoundsAmount >= workAreaEndBounds.X
            || windowPosition.Y + windowSize.Height * maximumAllowedOutOfBoundsAmount >= workAreaEndBounds.Y;

        return outOnTopLeftOfTheScreen || outOnBottomRightOfTheScreen;
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

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated || !IsWindowOutOfBounds(AppWindow)) return;
        CenterWindowOnScreen(AppWindow);
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        args.Handled = true;
        this.Hide();
        SaveWindowSettings(true);
    }
}
