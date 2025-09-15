using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Diagnostics.Tracing.Session;
using Application = Microsoft.UI.Xaml.Application;

namespace VolumeKeeper.Util;

public static class Util
{
    /**
     * Returns <b>true</b> if the current user is a member of the local Administrators group, even if the process is not running
     * with elevated privileges.
     */
    public static bool IsAdministrator()
    {
        using var windowsIdentity = WindowsIdentity.GetCurrent();
        var claims = new WindowsPrincipal(windowsIdentity).Claims;
        var adminClaimId = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Value;
        return claims.Any(c => c.Value == adminClaimId);
    }

    public static bool IsElevated() => TraceEventSession.IsElevated() == true;

    public static void EnsureAdminPrivileges()
    {
        if (IsElevated() || !IsAdministrator()) return;
        Console.WriteLine("Not running with elevated privileges. Attempting to restart as administrator...");
        RestartAsAdmin();
    }

    public static void RestartAsAdmin()
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
