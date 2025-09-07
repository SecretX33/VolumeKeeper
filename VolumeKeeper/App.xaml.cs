using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using H.NotifyIcon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using VolumeKeeper.Interop;
using VolumeKeeper.Services;
using Application = Microsoft.UI.Xaml.Application;

namespace VolumeKeeper;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private TaskbarIcon? _trayIcon;
    private static LoggingService? _loggingService;
    private AudioSessionManager? _audioSessionManager;
    private VolumeStorageService? _volumeStorageService;
    private VolumeMonitorService? _volumeMonitorService;
    private ApplicationMonitorService? _applicationMonitorService;
    private VolumeRestorationService? _volumeRestorationService;
    public static ILoggingService Logger => _loggingService ?? throw new InvalidOperationException("Logging service not initialized");
    public static AudioSessionManager? AudioSessionManager { get; private set; }
    public static VolumeStorageService? VolumeStorageService { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            // Initialize logging service first
            _loggingService = new LoggingService(DispatcherQueue.GetForCurrentThread());
            Logger.LogDebug("VolumeKeeper initialization started");

            // Initialize volume management services
            await InitializeServicesAsync();

            InitializeTrayIcon();
            ShowMainWindow();
        } catch (Exception ex)
        {
            Logger.LogError("Unhandled exception during application launch", ex, "App");
            ExitApplication();
        }
    }

    private void InitializeTrayIcon()
    {
        try {
            var openWindowCommand = (XamlUICommand)Resources["OpenWindowCommand"];
            openWindowCommand.ExecuteRequested += OpenWindowCommand_ExecuteRequested;

            var exitApplicationCommand = (XamlUICommand)Resources["ExitApplicationCommand"];
            exitApplicationCommand.ExecuteRequested += ExitApplicationCommand_ExecuteRequested;

            _trayIcon = (TaskbarIcon)Resources["TrayIcon"];
            _trayIcon.Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            _trayIcon.ForceCreate();
        }
        catch (Exception ex)
        {
            // If tray icon creation fails, we'll just run without it
            Logger.LogError("Failed to create tray icon", ex, "App");
            _trayIcon = null;
        }
    }

    private void OpenWindowCommand_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        ShowMainWindow();
    }

    private void ExitApplicationCommand_ExecuteRequested(object? _, ExecuteRequestedEventArgs args)
    {
        ExitApplication();
    }

    private void ShowMainWindow()
    {
        try
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow();
            }
            _mainWindow.ShowAndFocus();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to show main window", ex, "App");
        }
    }

    private async Task InitializeServicesAsync()
    {
        try
        {
            Logger.LogDebug("Initializing volume management services");

            // Initialize core services
            _audioSessionManager = new AudioSessionManager();
            AudioSessionManager = _audioSessionManager;

            _volumeStorageService = new VolumeStorageService();
            VolumeStorageService = _volumeStorageService;

            // Initialize monitoring services
            _volumeMonitorService = new VolumeMonitorService(_audioSessionManager, _volumeStorageService);
            _applicationMonitorService = new ApplicationMonitorService();

            await Task.WhenAll(
                _volumeMonitorService.InitializeAsync(),
                _applicationMonitorService.InitializeAsync()
            );

            _volumeRestorationService = new VolumeRestorationService(
                _audioSessionManager,
                _volumeStorageService,
                _applicationMonitorService);

            // Restore volumes for currently running applications
            _ = _volumeRestorationService.RestoreAllCurrentSessionsAsync();

            Logger.LogInfo("All services initialized successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to initialize services", ex, "App");
        }
    }

    private void ExitApplication()
    {
        Logger.LogDebug("VolumeKeeper application shutting down");

        // Ensure the application closes even if some services hang during disposal
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            Environment.Exit(0);
        });

        try
        {
            _mainWindow?.Close();
            _volumeMonitorService?.Dispose();
            _applicationMonitorService?.Dispose();
            _volumeRestorationService?.Dispose();
            _audioSessionManager?.Dispose();
            _trayIcon?.Dispose();
            _loggingService?.Dispose();
        }
        finally
        {
            Current.Exit();
        }
    }

}

