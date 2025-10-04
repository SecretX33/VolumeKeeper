using System;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace VolumeKeeper.Util;

public class Win32WindowHelper(Window window)
{
    private PointInt32? _minWindowSize;
    private PointInt32? _maxWindowSize;
    private NativeMethods.SubclassProc? _subclassProc;

    public void SetWindowMinMaxSize(
        PointInt32? minWindowSize = null,
        PointInt32? maxWindowSize = null
    )
    {
        _minWindowSize = minWindowSize;
        _maxWindowSize = maxWindowSize;

        var hwnd = WindowNative.GetWindowHandle(window);

        _subclassProc = WndProc; // Store subclasses procedure in field to prevent garbage collection
        NativeMethods.SetWindowSubclass(hwnd, _subclassProc, 0, 0);
    }

    private IntPtr WndProc(
        IntPtr hWnd,
        NativeMethods.WindowMessage Msg,
        UIntPtr wParam,
        IntPtr lParam,
        UIntPtr uIdSubclass,
        UIntPtr dwRefData
    ) {
        switch (Msg)
        {
            case NativeMethods.WindowMessage.WM_GETMINMAXINFO:
                var dpi = NativeMethods.GetDpiForWindow(hWnd);
                var scalingFactor = (float)dpi / 96;

                var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                if (_minWindowSize.HasValue)
                {
                    minMaxInfo.ptMinTrackSize.x = (int)(_minWindowSize.Value.X * scalingFactor);
                    minMaxInfo.ptMinTrackSize.y = (int)(_minWindowSize.Value.Y * scalingFactor);
                }
                if (_maxWindowSize.HasValue)
                {
                    minMaxInfo.ptMaxTrackSize.x = (int)(_maxWindowSize.Value.X * scalingFactor);
                    minMaxInfo.ptMaxTrackSize.y = (int)(_maxWindowSize.Value.Y * scalingFactor);
                }

                Marshal.StructureToPtr(minMaxInfo, lParam, true);
                break;
        }

        return NativeMethods.DefSubclassProc(hWnd, Msg, wParam, lParam);
    }

    // ReSharper disable InconsistentNaming
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    // ReSharper disable IdentifierTypo
    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }
}
