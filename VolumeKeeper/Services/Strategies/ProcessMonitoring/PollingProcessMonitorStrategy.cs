using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using VolumeKeeper.Util;

namespace VolumeKeeper.Services.Strategies.ProcessMonitoring;

/**
 * <p>A process monitoring strategy that uses polling to detect process start and stop events.
 * This strategy periodically checks the list of running processes and compares it to a known list.
 * It raises events when it detects new processes or when known processes have stopped.</p>
 *
 * <p><b>Note:</b> This is a fallback strategy and it's not as efficient or responsive as other strategies
 * like ETW or WMI. It is recommended to use this only if other strategies are unavailable.</p>
 */
public partial class PollingProcessMonitorStrategy : IProcessMonitorStrategy
{
    private Timer? _pollTimer;
    private readonly SemaphoreSlim _pollLock = new(1, 1);
    private readonly Dictionary<int, string> _knownProcesses = new();
    private volatile bool _isRunning;
    private readonly AtomicReference<bool> _isDisposed = new(false);

    public event EventHandler<ProcessEventArgs>? ProcessStarted;
    public event EventHandler<ProcessEventArgs>? ProcessStopped;

    public string Name => "Polling (Process.GetProcesses)";

    public bool Initialize()
    {
        try
        {
            Process.GetProcesses();
            App.Logger.LogInfo("Polling process monitor initialized successfully", "PollingProcessMonitorStrategy");
            return true;
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to initialize polling process monitor", ex, "PollingProcessMonitorStrategy");
            return false;
        }
    }

    public void Start()
    {
        if (_isRunning) return;

        _isRunning = true;

        InitializeKnownProcesses();

        _pollTimer = new Timer(PollForProcessChanges, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));

        App.Logger.LogInfo("Polling process monitor started", "PollingProcessMonitorStrategy");
    }

    private void InitializeKnownProcesses()
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
                    App.Logger.LogError($"Error processing initial process ID {process.Id}", ex, "PollingProcessMonitorStrategy");
                }
            }

            App.Logger.LogInfo($"Polling monitor initialized with {_knownProcesses.Count} known processes", "PollingProcessMonitorStrategy");
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to initialize known processes", ex, "PollingProcessMonitorStrategy");
        }
    }

    private async void PollForProcessChanges(object? state)
    {
        if (!_isRunning || _isDisposed.Get() || !await _pollLock.WaitAsync(0))
            return;

        try
        {
            var currentProcesses = Process.GetProcesses();
            var currentProcessMap = new Dictionary<int, string>();

            foreach (var process in currentProcesses)
            {
                try
                {
                    var executableName = GetExecutableName(process);
                    if (string.IsNullOrEmpty(executableName)) continue;

                    currentProcessMap[process.Id] = executableName;

                    if (!_knownProcesses.ContainsKey(process.Id))
                    {
                        _knownProcesses[process.Id] = executableName;
                        OnProcessStarted(executableName, process.Id);
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.LogError($"Error processing process ID {process.Id}", ex, "PollingProcessMonitorStrategy");
                }
            }

            var stoppedProcessIds = _knownProcesses.Keys.Where(id => !currentProcessMap.ContainsKey(id)).ToList();
            foreach (var processId in stoppedProcessIds)
            {
                if (_knownProcesses.TryGetValue(processId, out var processName))
                {
                    _knownProcesses.Remove(processId);
                    OnProcessStopped(processName, processId);
                }
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Error during process polling", ex, "PollingProcessMonitorStrategy");
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

    private void OnProcessStarted(string executableName, int processId)
    {
        ProcessStarted?.Invoke(this, new ProcessEventArgs
        {
            ProcessName = executableName,
            ProcessId = processId
        });
    }

    private void OnProcessStopped(string executableName, int processId)
    {
        ProcessStopped?.Invoke(this, new ProcessEventArgs
        {
            ProcessName = executableName,
            ProcessId = processId
        });
    }

    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;

        try
        {
            _pollTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            App.Logger.LogInfo("Polling process monitor stopped", "PollingProcessMonitorStrategy");
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Error stopping polling process monitor", ex, "PollingProcessMonitorStrategy");
        }
    }

    public void Dispose()
    {
        if (!_isDisposed.CompareAndSet(false, true))
            return;

        Stop();

        _pollTimer?.Dispose();
        _pollLock?.Dispose();
        _knownProcesses.Clear();
    }
}
