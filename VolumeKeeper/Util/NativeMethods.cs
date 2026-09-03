using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.Win32.SafeHandles;
using WinRT.Interop;

namespace VolumeKeeper.Util;

internal static partial class NativeMethods
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int MaximumProcessImagePathLength = 32768;

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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(
        uint processAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint processId
    );

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(
        SafeProcessHandle processHandle,
        uint flags,
        [Out] StringBuilder executablePath,
        ref uint size
    );

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

    public static string GetProcessImagePath(uint processId)
    {
        using var processHandle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (processHandle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        var executablePath = new StringBuilder(MaximumProcessImagePathLength);
        var size = (uint)executablePath.Capacity;
        if (!QueryFullProcessImageName(processHandle, 0, executablePath, ref size))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        return executablePath.ToString();
    }
}
