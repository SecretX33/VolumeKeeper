using System;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using H.NotifyIcon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.AppLifecycle;
using VolumeKeeper.Services;
using VolumeKeeper.Services.Log;
using VolumeKeeper.Services.Managers;
using VolumeKeeper.Util;
using static VolumeKeeper.Util.Util;
using Application = Microsoft.UI.Xaml.Application;

namespace VolumeKeeper;

public sealed partial class App : Application
{
    private readonly DispatcherQueue _mainThreadQueue = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("Failed to get main thread dispatcher queue");
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
            if (!await EnsureSingleInstance())
            {
                ExitApplication();
                return;
            }

            // Initialize logging service first
            _logger.Dispose();
            _logger = new FileLogger(_mainThreadQueue).Named();
            Logger.Debug("VolumeKeeper initialization started");

            ParseCommandLineArgs();
            await InitializeServicesAsync(_mainThreadQueue);

            InitializeTrayIcon();
            if (!_startMinimized) ShowMainWindow();
        } catch (Exception ex)
        {
            Logger.Error("Unhandled exception during application launch", ex);
            ExitApplication();
        }
    }

    private static async Task<bool> EnsureSingleInstance()
    {
        try
        {
            var mainInstance = AppInstance.FindOrRegisterForKey("VolumeKeeperInstance");
            if (mainInstance.IsCurrent)
            {
                // Subscribe to activation events to handle when other instances redirect to us
                mainInstance.Activated += OnActivated;
                return true;
            }
            Logger.Debug("Multiple instances detected. Bringing existing instance to front and exiting.");
            await BringExistingInstanceToFront(mainInstance);
            return false;
        }
        catch (Exception ex)
        {
            // If we can't create/check the app instance lock, allow this instance to run
            // Better to have multiple instances than to fail completely
            _logger.Debug("Failed to enforce single instance using AppInstance, allowing the app to run unconditionally", ex);
            return true;
        }
    }

    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        try
        {
            Logger.Debug("Bringing window back to foreground due to activation request from another instance");

            var app = (App)Current;
            var mainThreadQueue = app._mainThreadQueue;

            mainThreadQueue.TryEnqueueImmediate(() =>
            {
                app.ShowMainWindow();
            });
        } catch (Exception ex)
        {
            Logger.Error("Unhandled exception during window activation from another instance", ex);
        }
    }

    private static async Task BringExistingInstanceToFront(AppInstance appInstance)
    {
        try
        {
            var args = AppInstance.GetCurrent().GetActivatedEventArgs();
            await appInstance.RedirectActivationToAsync(args);
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
            _mainWindow.OnShow();
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to show main window", ex);
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
            Logger.Error("Failed to initialize services", ex);
        }
    }

    private void ExitApplication()
    {
        Logger.Debug("VolumeKeeper application shutting down");

        // Ensure the application closes even if some services hang during disposal
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1500)).ConfigureAwait(false);
            }
            finally
            {
                Environment.Exit(0);
            }
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
            Current.Exit();
        }
    }
}
