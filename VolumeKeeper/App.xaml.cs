using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
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
    private const string MutexName = "Global\\VolumeKeeper_SingleInstance_Mutex";
    private static Mutex? _singleInstanceMutex;
    private MainWindow? _mainWindow;
    private bool _startMinimized;
    private TaskbarIcon? _trayIcon;
    private static LoggingService? _loggingService;
    private static AudioSessionManager? _audioSessionManager;
    private static ProcessDataManager? _processDataManager;
    private static VolumeSettingsManager? _volumeSettingsManager;
    private static WindowSettingsManager? _windowSettingsManager;
    private static AudioSessionService? _audioSessionService;
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
            // Check for single instance
            if (!EnsureSingleInstance())
            {
                Console.WriteLine("Multiple instances detected. Bringing existing instance to front and exiting.");
                BringExistingInstanceToFront();
                ExitApplication();
                return;
            }

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

    private static bool EnsureSingleInstance()
    {
        try
        {
            // Try to create a new mutex
            _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);

            if (createdNew) return true;

            // Another instance already owns the mutex
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            return false;
        }
        catch (Exception)
        {
            // If we can't create/check the mutex, allow the instance to run
            // Better to have multiple instances than to fail completely
            return true;
        }
    }

    private static void BringExistingInstanceToFront()
    {
        try
        {
            // Find the existing VolumeKeeper process
            var currentProcess = Process.GetCurrentProcess();
            var anotherInstance = Process.GetProcessesByName(currentProcess.ProcessName)
                .FirstOrDefault(it => it.Id != currentProcess.Id);
            var handle = anotherInstance?.MainWindowHandle;

            if (handle == null || handle == IntPtr.Zero) return;

            NativeMethods.ShowAndFocus((IntPtr)handle);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to bring existing instance to front: " + ex.Message);
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
            _applicationMonitorService = new ApplicationMonitorService(_processDataManager);

            await Task.Run(_applicationMonitorService.Initialize);

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
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            Environment.Exit(0);
        });

        try
        {
            _mainWindow?.Close();
            DisposeAll(
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
            // Release the mutex before exiting
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;

            Current.Exit();
        }
    }

}

