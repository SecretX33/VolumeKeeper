using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using VolumeKeeper.Util;
using static VolumeKeeper.Util.Util;

namespace VolumeKeeper.Services.Strategies.ProcessMonitoring;

/**
 * <p>PollingProcessMonitorStrategy uses periodic polling with Process.GetProcesses() to detect process start and stop events.
 * This strategy maintains an internal list of known processes and periodically scans the system to detect changes,
 * comparing snapshots to identify new or terminated processes.</p>
 *
 * <p><b>Performance:</b> Higher resource consumption due to periodic full process enumeration. Detection latency
 * depends on polling interval (default: 2 seconds). CPU usage increases with the number of running processes
 * as each poll requires iterating through all system processes.</p>
 *
 * <p><b>Reliability:</b> Most reliable fallback strategy that works in all environments. Will eventually detect
 * all process changes but may miss very short-lived processes that start and stop between polling intervals.
 * Provides basic process information through standard .NET Process APIs.</p>
 *
 * <p><b>Note:</b> This is the ultimate fallback strategy that requires no special privileges or Windows features.
 * It will always work but should only be used when ETW and WMI strategies are unavailable due to its higher
 * resource usage and detection latency.</p>
 */
public partial class PollingProcessMonitorStrategy : IProcessMonitorStrategy
{
    private Timer? _pollTimer;
    private readonly TimeSpan _delayBeforeFirstPoll = TimeSpan.FromSeconds(1);
    private readonly TimeSpan _internalBetweenPolls = TimeSpan.FromSeconds(2);
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

        _pollTimer = new Timer(PollForProcessChanges, null, _delayBeforeFirstPoll, _internalBetweenPolls);

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

                    if (_knownProcesses.TryAdd(process.Id, executableName))
                    {
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

        try
        {
            Stop();
            DisposeAll(
                _pollTimer,
                _pollLock
            );
            _knownProcesses.Clear();
        }
        catch
        {
            /* Ignore exceptions during dispose */
        }

        GC.SuppressFinalize(this);
    }
}
