using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Diagnostics.Tracing.Session;
using Application = Microsoft.UI.Xaml.Application;

namespace VolumeKeeper.Util;

public static class Util
{
    public static bool IsElevated() => TraceEventSession.IsElevated() == true;

    public static bool IsAdministrator()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void EnsureAdminPrivileges()
    {
        if (IsElevated() || !IsAdministrator()) return;
        Console.WriteLine("Not running with elevated privileges. Attempting to restart as administrator...");
        RestartAsAdmin();
    }

    private static void RestartAsAdmin()
    {
        var exeName = Process.GetCurrentProcess().MainModule?.FileName;
        if (exeName == null)
        {
            MessageBox.Show("Unable to determine executable path for restarting as administrator.");
            return;
        }

        var startInfo = new ProcessStartInfo(exeName)
        {
            Verb = "runas", // This triggers the UAC prompt
            UseShellExecute = true
        };

        try
        {
            Console.WriteLine("Restarting with elevated privileges...");
            Process.Start(startInfo);
            Application.Current.Exit(); // Close current instance
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to restart as administrator: {ex.Message}");
        }
    }

    public static void DisposeAll(params IDisposable?[] disposables)
    {
        if (disposables.Length == 0) return;
        DisposeAll(disposables.AsEnumerable());
    }

    public static void DisposeAll(IEnumerable<IDisposable?> disposables)
    {
        var exceptions = new List<Exception>();

        foreach (var disposable in disposables)
        {
            if (disposable == null) continue;
            try { disposable.Dispose(); }
            catch (Exception ex) { exceptions.Add(ex); }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }
}
