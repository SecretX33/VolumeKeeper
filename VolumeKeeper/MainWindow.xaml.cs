using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;

namespace VolumeKeeper;

public sealed partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

    public MainWindow()
    {
        InitializeComponent();

        Title = "VolumeKeeper";
        ExtendsContentIntoTitleBar = true;

        NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems[0];
        NavigateToPage("Home");

        Closed += MainWindow_Closed;
    }

    private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            if (!string.IsNullOrEmpty(tag))
            {
                NavigateToPage(tag);
            }
        }
    }

    private void NavigateToPage(string tag)
    {
        Type? pageType = tag switch
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

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        args.Handled = true;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ShowWindow(hwnd, 0); // SW_HIDE
    }
}
