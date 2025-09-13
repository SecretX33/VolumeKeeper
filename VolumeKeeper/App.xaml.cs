using System;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using H.NotifyIcon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using VolumeKeeper.Services;
using VolumeKeeper.Services.Managers;
using VolumeKeeper.Util;
using static VolumeKeeper.Util.Util;
using Application = Microsoft.UI.Xaml.Application;

namespace VolumeKeeper;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private bool _startMinimized;
    private TaskbarIcon? _trayIcon;
    private static LoggingService? _loggingService;
    private static AudioSessionManager? _audioSessionManager;
    private static ProcessDataManager? _processDataManager;
    private static VolumeSettingsManager? _volumeSettingsManager;
    private static WindowSettingsManager? _windowSettingsManager;
    private static AudioSessionService? _audioSessionService;
    private static VolumeMonitorService? _volumeMonitorService;
    private ApplicationMonitorService? _applicationMonitorService;
    private VolumeRestorationService? _volumeRestorationService;
    public static ILoggingService Logger => _loggingService ?? throw new InvalidOperationException("Logging service not initialized");
    public static AudioSessionManager AudioSessionManager => _audioSessionManager ?? throw new InvalidOperationException("Audio session manager not initialized");
    public static VolumeSettingsManager VolumeSettingsManager => _volumeSettingsManager ?? throw new InvalidOperationException("Volume settings manager not initialized");
    public static WindowSettingsManager WindowSettingsManager => _windowSettingsManager ?? throw new InvalidOperationException("Window settings service not initialized");
    public static AudioSessionService AudioSessionService => _audioSessionService ?? throw new InvalidOperationException("Audio session manager not initialized");

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
            ParseCommandLineArgs();

            // Initialize volume management services
            await InitializeServicesAsync();

            InitializeTrayIcon();
            if (!_startMinimized) ShowMainWindow();
        } catch (Exception ex)
        {
            Logger.LogError("Unhandled exception during application launch", ex, "App");
            ExitApplication();
        }
    }

    private void ParseCommandLineArgs()
    {
        var commandLineArgs = Environment.GetCommandLineArgs();
        _startMinimized = Array.Exists(commandLineArgs, arg =>
            arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

        if (_startMinimized)
        {
            Logger.LogDebug("Starting in minimized mode");
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

            // Initialize data managers
            _audioSessionManager = new AudioSessionManager();
            _processDataManager = new ProcessDataManager();
            _volumeSettingsManager = new VolumeSettingsManager();
            _windowSettingsManager = new WindowSettingsManager();

            await Task.WhenAll(
                Task.Run(_volumeSettingsManager.InitializeAsync),
                Task.Run(_windowSettingsManager.InitializeAsync)
            );

            // Initialize core services with managers
            _audioSessionService = new AudioSessionService(_audioSessionManager);

            // Initialize monitoring services with managers
            _volumeMonitorService = new VolumeMonitorService(_audioSessionManager, _volumeSettingsManager);
            _applicationMonitorService = new ApplicationMonitorService(_processDataManager);

            await Task.WhenAll(
                Task.Run(_applicationMonitorService.Initialize),
                Task.Run(_volumeMonitorService.Initialize)
            );

            _volumeRestorationService = new VolumeRestorationService(
                _audioSessionService,
                _audioSessionManager,
                _volumeSettingsManager,
                _applicationMonitorService
            );

            // Restore volumes for currently running applications
            _ = Task.Run(_volumeRestorationService.RestoreAllCurrentSessionsAsync);

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
            DisposeAll(
                _volumeMonitorService,
                _applicationMonitorService,
                _volumeRestorationService,
                _audioSessionManager,
                _audioSessionService,
                _trayIcon,
                _loggingService
            );
        }
        finally
        {
            Current.Exit();
        }
    }

}

