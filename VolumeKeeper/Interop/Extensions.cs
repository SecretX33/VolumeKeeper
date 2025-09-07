using System;
using Microsoft.UI.Xaml;

namespace VolumeKeeper.Interop;

public static class Extensions
{
    public static void ShowAndFocus(this Window window)
    {
        window = window ?? throw new ArgumentNullException(nameof(window));

        // Ensure the operation runs on the UI thread
        if (window.DispatcherQueue.HasThreadAccess)
        {
            NativeMethods.ShowAndFocus(window);
        }
        else
        {
            window.DispatcherQueue.TryEnqueue(() => NativeMethods.ShowAndFocus(window));
        }
    }
}
