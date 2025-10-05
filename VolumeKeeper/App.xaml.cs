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
using VolumeKeeper.Services.Log;
using VolumeKeeper.Services.Managers;
using VolumeKeeper.Util;
using static VolumeKeeper.Util.Util;
using Application = Microsoft.UI.Xaml.Application;

namespace VolumeKeeper;

public sealed partial class App : Application
{
    private const string MutexName = "Global\\VolumeKeeper_SingleInstance_Mutex";
    private static Mutex? _singleInstanceMutex;
    private MainWindow? _mainWindow;
    private bool _startMinimized;
    private TaskbarIcon? _trayIcon;
    private static Logger _logger = new ConsoleLogger().Named();
    private static AudioSessionManager? _audioSessionManager;
    private static VolumeSettingsManager? _volumeSettingsManager;
    private static WindowSettingsManager? _windowSettingsManager;
    private static AudioSessionService? _audioSessionService;
    public static Logger Logger => _logger ?? throw new InvalidOperationException("Logging service not initialized");
    public static AudioSessionManager AudioSessionManager => _audioSessionManager ?? throw new InvalidOperationException("Audio session manager not initialized");
    public static VolumeSettingsManager VolumeSettingsManager => _volumeSettingsManager ?? throw new InvalidOperationException("Volume settings manager not initialized");
    public static WindowSettingsManager WindowSettingsManager => _windowSettingsManager ?? throw new InvalidOperationException("Window settings service not initialized");
    public static AudioSessionService AudioSessionService => _audioSessionService ?? throw new InvalidOperationException("Audio session service not initialized");

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
                Logger.Debug("Multiple instances detected. Bringing existing instance to front and exiting.");
                BringExistingInstanceToFront();
                ExitApplication();
                return;
            }
            var mainThreadQueue = DispatcherQueue.GetForCurrentThread();

            // Initialize logging service first
            _logger.Dispose();
            _logger = new FileLogger(mainThreadQueue).Named();
            Logger.Debug("VolumeKeeper initialization started");

            ParseCommandLineArgs();
            await InitializeServicesAsync(mainThreadQueue);

            InitializeTrayIcon();
            if (!_startMinimized) ShowMainWindow();
        } catch (Exception ex)
        {
            Logger.Error("Unhandled exception during application launch", ex, "App");
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
            Logger.Error("Failed to bring existing instance to front", ex);
        }
    }

    private void ParseCommandLineArgs()
    {
        var commandLineArgs = Environment.GetCommandLineArgs();
        _startMinimized = Array.Exists(commandLineArgs, arg =>
            arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

        if (_startMinimized)
        {
            Logger.Debug("Starting in minimized mode");
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
            Logger.Error("Failed to create tray icon", ex, "App");
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
            Logger.Error("Failed to show main window", ex, "App");
        }
    }

    private async Task InitializeServicesAsync(DispatcherQueue mainThreadQueue)
    {
        try
        {
            Logger.Debug("Initializing volume management services");

            // Initialize settings managers first
            var iconService = new IconService(mainThreadQueue);

            _volumeSettingsManager = new VolumeSettingsManager();
            _windowSettingsManager = new WindowSettingsManager();

            await Task.WhenAll(
                Task.Run(_volumeSettingsManager.InitializeAsync),
                Task.Run(_windowSettingsManager.InitializeAsync)
            );

            _audioSessionManager = new AudioSessionManager(iconService, _volumeSettingsManager, mainThreadQueue);
            await Task.Run(_audioSessionManager.Initialize);

            // Initialize core services with managers
            _audioSessionService = new AudioSessionService(_audioSessionManager, mainThreadQueue);

            Logger.Debug("All services initialized successfully");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to initialize services", ex, "App");
        }
    }

    private void ExitApplication()
    {
        Logger.Debug("VolumeKeeper application shutting down");

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
                _audioSessionManager,
                _audioSessionService,
                _trayIcon,
                _logger
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

