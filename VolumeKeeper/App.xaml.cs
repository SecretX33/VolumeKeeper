using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VolumeKeeper.Services;
using Microsoft.UI.Dispatching;

namespace VolumeKeeper;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private TaskbarIcon? _trayIcon;
    private static LoggingService? _loggingService;

    public bool HasTrayIcon => _trayIcon != null;
    public static ILoggingService Logger => _loggingService ?? throw new InvalidOperationException("Logging service not initialized");

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Initialize logging service first
        _loggingService = new LoggingService(DispatcherQueue.GetForCurrentThread());
        Logger.LogInfo("VolumeKeeper application starting");

        _mainWindow = new MainWindow();

        CreateTrayIcon();

        _mainWindow.Activate();
    }

    private void CreateTrayIcon()
    {
        try
        {
            _trayIcon = new TaskbarIcon();
            _trayIcon.ToolTipText = "VolumeKeeper";

            // Create a simple icon using System.Drawing
            _trayIcon.Icon = CreateSimpleIcon();

            var contextMenu = new MenuFlyout();

            var openItem = new MenuFlyoutItem { Text = "Open VolumeKeeper" };
            openItem.Click += (_, _) => ShowMainWindow();
            contextMenu.Items.Add(openItem);

            contextMenu.Items.Add(new MenuFlyoutSeparator());

            var exitItem = new MenuFlyoutItem { Text = "Exit" };
            exitItem.Click += (_, _) => ExitApplication();
            contextMenu.Items.Add(exitItem);

            _trayIcon.ContextFlyout = contextMenu;
            _trayIcon.LeftClickCommand = new RelayCommand(ShowMainWindow);

            // Make sure the icon is visible
            _trayIcon.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            // If tray icon creation fails, we'll just run without it
            Logger.LogError("Failed to create tray icon", ex, "App");
            _trayIcon = null;
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow != null)
        {
            _mainWindow.Activate();

            // Get window handle and show window
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, 5); // SW_SHOW
            }
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private void ExitApplication()
    {
        _trayIcon?.Dispose();
        _mainWindow?.Close();
        _loggingService?.Dispose();
        Environment.Exit(0);
    }

    private Icon CreateSimpleIcon()
    {
        // Create a simple 16x16 icon with a volume symbol
        var bitmap = new Bitmap(16, 16);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);

            // Draw a simple speaker shape
            using (var brush = new SolidBrush(Color.White))
            {
                // Speaker base
                graphics.FillRectangle(brush, 2, 6, 4, 4);
                // Speaker cone
                graphics.FillPolygon(brush, new Point[] {
                    new Point(6, 4), new Point(6, 12), new Point(10, 14), new Point(10, 2)
                });
                // Sound waves
                graphics.DrawArc(new Pen(Color.White, 1), 11, 5, 4, 6, -30, 60);
            }
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }
}

public partial class RelayCommand : ICommand
{
    private readonly Action _execute;

    public RelayCommand(Action execute)
    {
        _execute = execute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute();
}
