using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace VolumeKeeper.Util;

internal static partial class NativeMethods
{
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, ShowWindowCommand nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("comctl32.dll", SetLastError = true)]
    public static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, nuint uIdSubclass, nuint dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    public static extern nint DefSubclassProc(IntPtr hWnd, WindowMessage Msg, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hwnd);

    public delegate nint SubclassProc(IntPtr hWnd, WindowMessage Msg, UIntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData);

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

    [Flags]
    public enum WindowLongIndexFlags : int
    {
        GWL_WNDPROC = -4,
    }

    public enum WindowMessage : int
    {
        WM_GETMINMAXINFO = 0x0024,
    }

    public static void ShowAndFocus(Window window) => ShowAndFocus(WindowNative.GetWindowHandle(window));

    public static void ShowAndFocus(IntPtr hWnd)
    {
        ShowWindow(hWnd, ShowWindowCommand.SW_RESTORE);  // Show and restore if minimized
        SetForegroundWindow(hWnd);  // Bring to foreground
    }
}
