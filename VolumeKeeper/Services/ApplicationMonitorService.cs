using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VolumeKeeper.Services.Managers;
using VolumeKeeper.Services.Strategies.ProcessMonitoring;
using VolumeKeeper.Util;

namespace VolumeKeeper.Services;

public class ApplicationLaunchEventArgs : EventArgs
{
    public string ExecutableName { get; init; } = string.Empty;
    public int ProcessId { get; init; }
}

public class ApplicationMonitorService : IDisposable
{
    private readonly ProcessDataManager _processDataManager;
    private readonly AtomicReference<bool> _isDisposed = new(false);
    private IProcessMonitorStrategy? _activeStrategy;

    public event EventHandler<ApplicationLaunchEventArgs>? ApplicationLaunched;

    public ApplicationMonitorService(ProcessDataManager processDataManager)
    {
        _processDataManager = processDataManager;
        InitializeStrategy();
    }

    private void InitializeStrategy()
    {
        var strategies = new List<IProcessMonitorStrategy>
        {
            new EtwProcessMonitorStrategy(),
            new WmiProcessMonitorStrategy(),
            new PollingProcessMonitorStrategy()
        };

        foreach (var strategy in strategies)
        {
            try
            {
                if (!strategy.Initialize())
                {
                    strategy.Dispose();
                    continue;
                }

                _activeStrategy = strategy;
                _activeStrategy.ProcessStarted += OnProcessStarted;
                _activeStrategy.ProcessStopped += OnProcessStopped;
                _activeStrategy.Start();

                App.Logger.LogInfo($"Using process monitor strategy: {strategy.Name}", "ApplicationMonitorService");

                foreach (var unusedStrategy in strategies.Where(s => s != _activeStrategy))
                {
                    unusedStrategy.Dispose();
                }

                return;
            }
            catch (Exception ex)
            {
                App.Logger.LogError($"Failed to initialize strategy: {strategy.Name}", ex, "ApplicationMonitorService");
                strategy.Dispose();
            }
        }

        App.Logger.LogError("Failed to initialize any process monitoring strategy", null, "ApplicationMonitorService");
    }

    private void OnProcessStarted(object? sender, ProcessEventArgs e)
    {
        try
        {
            if (!_processDataManager.IsProcessKnown(e.ProcessId))
            {
                _processDataManager.AddProcess(e.ProcessId, e.ProcessName);
                OnApplicationLaunched(e.ProcessName, e.ProcessId);
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError($"Error handling process start event for {e.ProcessName} (PID: {e.ProcessId})", ex, "ApplicationMonitorService");
        }
    }

    private void OnProcessStopped(object? sender, ProcessEventArgs e)
    {
        try
        {
            _processDataManager.RemoveProcess(e.ProcessId);
            App.Logger.LogInfo($"Application stopped: {e.ProcessName} (PID: {e.ProcessId})", "ApplicationMonitorService");
        }
        catch (Exception ex)
        {
            App.Logger.LogError($"Error handling process stop event for {e.ProcessName} (PID: {e.ProcessId})", ex, "ApplicationMonitorService");
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

    public void Initialize()
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
                        _processDataManager.AddProcess(process.Id, executableName);
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.LogError($"Error processing process ID {process.Id}", ex,
                        "ApplicationMonitorService");
                }
            }

            App.Logger.LogInfo($"Application monitor initialized with {_processDataManager.Count} known processes",
                "ApplicationMonitorService");
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to initialize application monitor", ex, "ApplicationMonitorService");
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

    public void Dispose()
    {
        if (!_isDisposed.CompareAndSet(false, true))
            return;

        if (_activeStrategy != null)
        {
            _activeStrategy.ProcessStarted -= OnProcessStarted;
            _activeStrategy.ProcessStopped -= OnProcessStopped;
            _activeStrategy.Stop();
            _activeStrategy.Dispose();
        }
    }
}
