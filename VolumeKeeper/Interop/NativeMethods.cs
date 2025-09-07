using System;
using System.Drawing;
using System.Runtime.InteropServices;
using H.NotifyIcon.Core;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace VolumeKeeper.Interop;

internal static class NativeMethods
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommand nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private enum ShowWindowCommand
    {
        SW_HIDE = 0,
        SW_SHOWNORMAL = 1,
        SW_SHOWMINIMIZED = 2,
        SW_MAXIMIZE = 3,
        SW_SHOWNOACTIVATE = 4,
        SW_SHOW = 5,
        SW_MINIMIZE = 6,
        SW_SHOWMINNOACTIVE = 7,
        SW_SHOWNA = 8,
        SW_RESTORE = 9,
        SW_SHOWDEFAULT = 10,
        SW_FORCEMINIMIZE = 11,
    }

    public static Icon? ExtractIconFromFile(string filePath)
    {
        IntPtr hIcon = ExtractIcon(IntPtr.Zero, filePath, 0);
        if (hIcon == IntPtr.Zero) return null;
        return Icon.FromHandle(hIcon);
    }

    public static void ShowAndFocus(Window window)
    {
        IntPtr hWnd = WindowNative.GetWindowHandle(window);
        ShowWindow(hWnd, ShowWindowCommand.SW_RESTORE);  // Show and restore if minimized
        SetForegroundWindow(hWnd);  // Bring to foreground
    }
}
