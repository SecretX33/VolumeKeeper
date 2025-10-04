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
    private readonly PointInt32 _minWindowSize = new(400, 300);
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
                X = (int)windowSettings.X,
                Y = (int)windowSettings.Y
            };
            appWindow.Move(position);

            // Check if window is out of bounds after moving
            // if (IsWindowOutOfBounds(appWindow))
            // {
                // CenterWindowOnScreen(appWindow);
                // SaveWindowSettings(true);
            // }
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

    private bool IsWindowOutOfBounds(AppWindow appWindow)
    {
        // Get all display areas to check if window is visible on any monitor
        var displays = DisplayArea.FindAll();
        if (displays == null || displays.Count == 0) return false;

        var windowPosition = appWindow.Position;
        var windowSize = appWindow.Size;

        // Check if window is within any display's work area
        foreach (var display in displays)
        {
            var workArea = display.WorkArea;

            // Window is considered in bounds if its top-left corner is within the work area
            if (windowPosition.X >= workArea.X &&
                windowPosition.X < workArea.X + workArea.Width &&
                windowPosition.Y >= workArea.Y &&
                windowPosition.Y < workArea.Y + workArea.Height)
            {
                return false; // Window is in bounds
            }
        }

        return true; // Window is out of bounds
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
