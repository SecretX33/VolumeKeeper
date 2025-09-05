using System;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace VolumeKeeper;

public sealed partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

    private WindowSettings _windowSettings = null!;

    public MainWindow()
    {
        InitializeComponent();

        Title = "VolumeKeeper";
        ExtendsContentIntoTitleBar = true;

        LoadWindowSettings();

        NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems[0];
        NavigateToPage("Home");

        Closed += MainWindow_Closed;
        SizeChanged += MainWindow_SizeChanged;

        // Track position changes
        this.Activated += MainWindow_Activated;

        // Track window state changes
        var presenter = AppWindow.Presenter as OverlappedPresenter;
        if (presenter == null) return;

        presenter.IsMaximizable = true;
        presenter.IsMinimizable = true;
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
        _windowSettings = WindowSettings.Load();

        // Set window size using AppWindow
        var appWindow = this.AppWindow;
        if (appWindow == null) return;

        // Apply maximize state if needed
        var presenter = appWindow.Presenter as OverlappedPresenter;
        if (_windowSettings.IsMaximized && presenter != null)
        {
            presenter.Maximize();
            return;
        }

        var size = new SizeInt32
        {
            Width = (int)_windowSettings.Width,
            Height = (int)_windowSettings.Height
        };
        appWindow.Resize(size);

        // Set window position if we have saved values, otherwise center on screen
        if (!double.IsNaN(_windowSettings.X) && !double.IsNaN(_windowSettings.Y))
        {
            var position = new PointInt32
            {
                X = (int)_windowSettings.X,
                Y = (int)_windowSettings.Y
            };
            appWindow.Move(position);
        }
        else
        {
            // Center the window on the primary display
            CenterWindowOnScreen(appWindow);
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

    private void SaveWindowSettings()
    {
        // Get current window state using AppWindow
        var appWindow = AppWindow;
        if (appWindow == null) return;

        // Check if window is maximized
        var presenter = appWindow.Presenter as OverlappedPresenter;
        _windowSettings.IsMaximized = presenter?.State == OverlappedPresenterState.Maximized;

        // Only save size and position if not maximized
        if (!_windowSettings.IsMaximized)
        {
            _windowSettings.X = appWindow.Position.X;
            _windowSettings.Y = appWindow.Position.Y;
            _windowSettings.Width = appWindow.Size.Width;
            _windowSettings.Height = appWindow.Size.Height;
        }

        _windowSettings.Save();
    }

    private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        // Save size changes with a small delay to avoid excessive saves during dragging
        SaveWindowSettings();
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        // Save position when window is activated (after moving)
        SaveWindowSettings();
    }

    void Hide()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        ShowWindow(hwnd, 0); // SW_HIDE
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        SaveWindowSettings();
        Application.Current.Exit();
    }

}
