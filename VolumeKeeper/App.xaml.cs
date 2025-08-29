using Microsoft.UI.Xaml;
using System;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;

namespace VolumeKeeper;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private TaskbarIcon? _trayIcon;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow = new MainWindow();

        CreateTrayIcon();

        _mainWindow.Activate();
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new TaskbarIcon();
        _trayIcon.ToolTipText = "VolumeKeeper";

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
    }

    private void ShowMainWindow()
    {
        _mainWindow?.Activate();
        _mainWindow?.Show();
    }

    private void ExitApplication()
    {
        _trayIcon?.Dispose();
        _mainWindow?.Close();
        Environment.Exit(0);
    }
}

public partial class RelayCommand : System.Windows.Input.ICommand
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
