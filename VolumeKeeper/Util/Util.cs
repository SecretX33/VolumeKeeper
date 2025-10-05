using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Diagnostics.Tracing.Session;
using VolumeKeeper.Models;
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
        App.Logger.Debug("Not running with elevated privileges. Attempting to restart as administrator...");
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
            App.Logger.Debug("Restarting with elevated privileges...");
            Process.Start(startInfo);
            Application.Current.Exit(); // Close current instance
        }
        catch (Exception ex)
        {
            App.Logger.Error("Failed to restart as administrator", ex, "Util");
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

    public static ProcessInfo? GetProcessInfoOrNull(int processId)
    {
        if (processId <= 0) return null;
        try
        {
            using var process = Process.GetProcessById(processId);
            var fullPath = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(fullPath)) return null;

            var info = GetFileVersionInfoOrNull(processId, fullPath);
            var executableName = Path.GetFileName(fullPath);
            var displayName = new[]
                {
                    process.MainWindowTitle,
                    info?.FileDescription,
                    process.MainModule?.ModuleName
                }
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                ?? executableName;

            return new ProcessInfo(
                Id: processId,
                DisplayName: displayName,
                ExecutableName: executableName,
                ExecutablePath: fullPath
            );
        }
        catch
        {
            return null;
        }
    }

    private static FileVersionInfo? GetFileVersionInfoOrNull(
        int processId,
        string executablePath
    ) {
        try
        {
            return FileVersionInfo.GetVersionInfo(executablePath);
        }
        catch (Exception ex)
        {
            App.Logger.Debug($"Failed to get file version info for process {processId} at path: {executablePath}", ex, "Util");
            return null;
        }
    }
}
