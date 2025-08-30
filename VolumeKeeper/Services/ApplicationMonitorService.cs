using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace VolumeKeeper.Services;

public class ApplicationLaunchEventArgs : EventArgs
{
    public string ExecutableName { get; init; } = string.Empty;
    public int ProcessId { get; init; }
}

public class ApplicationMonitorService : IDisposable
{
    private readonly ConcurrentDictionary<int, string> _knownProcesses = new();
    private readonly Timer _pollTimer;
    private readonly SemaphoreSlim _pollLock = new(1, 1);
    private ManagementEventWatcher? _processWatcher;
    private bool _isDisposed;

    public event EventHandler<ApplicationLaunchEventArgs>? ApplicationLaunched;

    public ApplicationMonitorService()
    {
        _pollTimer = new Timer(PollForNewProcesses, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        InitializeWmiWatcher();
    }

    private void InitializeWmiWatcher()
    {
        try
        {
            var query = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
            _processWatcher = new ManagementEventWatcher(query);
            _processWatcher.EventArrived += OnProcessStarted;
            _processWatcher.Start();
            App.Logger.LogInfo("WMI process watcher started", "ApplicationMonitorService");
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to initialize WMI watcher, falling back to polling only", ex, "ApplicationMonitorService");
        }
    }

    private void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var processName = e.NewEvent.Properties["ProcessName"].Value?.ToString();
            var processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);

            if (!string.IsNullOrEmpty(processName) && !_knownProcesses.ContainsKey(processId))
            {
                _knownProcesses[processId] = processName;
                OnApplicationLaunched(processName, processId);
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Error handling WMI process start event", ex, "ApplicationMonitorService");
        }
    }

    private async void PollForNewProcesses(object? state)
    {
        if (_isDisposed || !await _pollLock.WaitAsync(0))
            return;

        try
        {
            var currentProcesses = Process.GetProcesses();

            foreach (var process in currentProcesses)
            {
                try
                {
                    if (_knownProcesses.ContainsKey(process.Id) || string.IsNullOrEmpty(process.ProcessName)) continue;

                    var executableName = GetExecutableName(process);
                    if (!string.IsNullOrEmpty(executableName))
                    {
                        _knownProcesses[process.Id] = executableName;
                        OnApplicationLaunched(executableName, process.Id);
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.LogError($"Error processing process ID {process.Id}", ex, "ApplicationMonitorService");
                }
            }

            var currentIds = new HashSet<int>(currentProcesses.Select(p => p.Id));
            var toRemove = _knownProcesses.Keys.Where(id => !currentIds.Contains(id)).ToList();
            foreach (var id in toRemove)
            {
                _knownProcesses.TryRemove(id, out _);
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Error during process polling", ex, "ApplicationMonitorService");
        }
        finally
        {
            _pollLock.Release();
        }
    }

    private string GetExecutableName(Process process)
    {
        try
        {
            return Path.GetFileName(process.MainModule?.FileName ?? process.ProcessName);
        }
        catch
        {
            return process.ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? process.ProcessName
                : $"{process.ProcessName}.exe";
        }
    }

    private void OnApplicationLaunched(string executableName, int processId)
    {
        App.Logger.LogInfo($"Application launched: {executableName} (PID: {processId})", "ApplicationMonitorService");
        ApplicationLaunched?.Invoke(this, new ApplicationLaunchEventArgs
        {
            ExecutableName = executableName,
            ProcessId = processId
        });
    }

    public Task InitializeAsync()
    {
        try
        {
            var processes = Process.GetProcesses();
            foreach (var process in processes)
            {
                try
                {
                    var executableName = GetExecutableName(process);
                    if (!string.IsNullOrEmpty(executableName))
                    {
                        _knownProcesses[process.Id] = executableName;
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.LogError($"Error processing process ID {process.Id}", ex, "ApplicationMonitorService");
                }
            }
            App.Logger.LogInfo($"Application monitor initialized with {_knownProcesses.Count} known processes", "ApplicationMonitorService");
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to initialize application monitor", ex, "ApplicationMonitorService");
        }
        
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _processWatcher?.Stop();
        _processWatcher?.Dispose();
        _pollTimer?.Dispose();
        _pollLock?.Dispose();
    }
}
