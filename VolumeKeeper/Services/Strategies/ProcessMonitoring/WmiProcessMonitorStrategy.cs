using System;
using System.IO;
using System.Management;

namespace VolumeKeeper.Services.Strategies.ProcessMonitoring;

public partial class WmiProcessMonitorStrategy : IProcessMonitorStrategy
{
    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;
    private volatile bool _isRunning;
    private volatile bool _isDisposed;

    public event EventHandler<ProcessEventArgs>? ProcessStarted;
    public event EventHandler<ProcessEventArgs>? ProcessStopped;

    public string Name => "WMI (Windows Management Instrumentation)";

    public bool Initialize()
    {
        try
        {
            var startQuery = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
            _startWatcher = new ManagementEventWatcher(startQuery);
            _startWatcher.EventArrived += OnProcessStarted;

            var stopQuery = new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace");
            _stopWatcher = new ManagementEventWatcher(stopQuery);
            _stopWatcher.EventArrived += OnProcessStopped;

            _startWatcher.Start();
            _startWatcher.Stop();

            App.Logger.LogInfo("WMI process monitor initialized successfully", "WmiProcessMonitorStrategy");
            return true;
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to initialize WMI process monitor", ex, "WmiProcessMonitorStrategy");
            DisposeWatchers();
            return false;
        }
    }

    public void Start()
    {
        if (_isRunning || _startWatcher == null || _stopWatcher == null) return;

        try
        {
            _startWatcher.Start();
            _stopWatcher.Start();
            _isRunning = true;
            App.Logger.LogInfo("WMI process monitor started", "WmiProcessMonitorStrategy");
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to start WMI process monitor", ex, "WmiProcessMonitorStrategy");
            Stop();
        }
    }

    private void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var processName = e.NewEvent.Properties["ProcessName"].Value?.ToString();
            var processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);

            if (!string.IsNullOrEmpty(processName))
            {
                var executableName = GetExecutableName(processName);
                ProcessStarted?.Invoke(this, new ProcessEventArgs
                {
                    ProcessName = executableName,
                    ProcessId = processId
                });
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Error handling WMI process start event", ex, "WmiProcessMonitorStrategy");
        }
    }

    private void OnProcessStopped(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var processName = e.NewEvent.Properties["ProcessName"].Value?.ToString();
            var processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);

            if (!string.IsNullOrEmpty(processName))
            {
                var executableName = GetExecutableName(processName);
                ProcessStopped?.Invoke(this, new ProcessEventArgs
                {
                    ProcessName = executableName,
                    ProcessId = processId
                });
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Error handling WMI process stop event", ex, "WmiProcessMonitorStrategy");
        }
    }

    private string GetExecutableName(string processName)
    {
        try
        {
            if (Path.HasExtension(processName))
            {
                return Path.GetFileName(processName);
            }

            return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName
                : $"{processName}.exe";
        }
        catch
        {
            return processName;
        }
    }

    public void Stop()
    {
        if (!_isRunning) return;

        try
        {
            _startWatcher?.Stop();
            _stopWatcher?.Stop();
            _isRunning = false;
            App.Logger.LogInfo("WMI process monitor stopped", "WmiProcessMonitorStrategy");
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Error stopping WMI process monitor", ex, "WmiProcessMonitorStrategy");
        }
    }

    private void DisposeWatchers()
    {
        try
        {
            if (_startWatcher != null)
            {
                _startWatcher.EventArrived -= OnProcessStarted;
                _startWatcher.Dispose();
                _startWatcher = null;
            }

            if (_stopWatcher != null)
            {
                _stopWatcher.EventArrived -= OnProcessStopped;
                _stopWatcher.Dispose();
                _stopWatcher = null;
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Error disposing WMI watchers", ex, "WmiProcessMonitorStrategy");
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        Stop();
        DisposeWatchers();
    }
}
