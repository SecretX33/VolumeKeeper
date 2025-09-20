using System;
using System.Management;
using VolumeKeeper.Util;

namespace VolumeKeeper.Services.Strategies.ProcessMonitoring;

/**
 * <p>WmiProcessMonitorStrategy uses Windows Management Instrumentation (WMI) to monitor process start and stop events.
 * WMI provides a standardized way to access management information through event subscriptions to Win32_ProcessStartTrace
 * and Win32_ProcessStopTrace events.</p>
 *
 * <p><b>Performance:</b> Moderate performance with higher overhead than ETW. Event delivery may have slight delays
 * as WMI operates at a higher level than kernel events. Suitable for most monitoring scenarios where real-time
 * response is not critical.</p>
 *
 * <p><b>Reliability:</b> Generally reliable for standard process monitoring. May occasionally miss very short-lived
 * processes due to the event subscription model. Provides basic process information including process name and PID.</p>
 *
 * <p><b>Note:</b> This strategy works without administrator privileges in most cases, making it a good fallback
 * when ETW is unavailable. It is the recommended strategy for non-elevated applications.</p>
 */
public partial class WmiProcessMonitorStrategy : IProcessMonitorStrategy
{
    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;
    private readonly AtomicReference<bool> _isRunning = new(false);
    private readonly AtomicReference<bool> _isDisposed = new(false);

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
            _stopWatcher.Start();

            App.Logger.LogDebug("WMI process monitor initialized successfully", "WmiProcessMonitorStrategy");
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
        if (!_isRunning.CompareAndSet(false, true) || _startWatcher == null || _stopWatcher == null) return;

        try
        {
            _startWatcher.Start();
            _stopWatcher.Start();
            App.Logger.LogDebug("WMI process monitor started", "WmiProcessMonitorStrategy");
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
            HandleEvent(ProcessStarted, e);
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
            HandleEvent(ProcessStopped, e);
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Error handling WMI process stop event", ex, "WmiProcessMonitorStrategy");
        }
    }

    private void HandleEvent(EventHandler<ProcessEventArgs>? eventHandler, EventArrivedEventArgs e)
    {
        if (eventHandler == null) return;

        var processName = e.NewEvent.Properties["ProcessName"].Value?.ToString();
        if (string.IsNullOrEmpty(processName)) return;

        var processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);

        eventHandler(this, new ProcessEventArgs
        {
            ExecutableName = processName,
            Id = processId
        });
    }

    public void Stop()
    {
        if (!_isRunning.CompareAndSet(true, false)) return;

        try
        {
            _startWatcher?.Stop();
            _stopWatcher?.Stop();
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
                _startWatcher.Stop();
                _startWatcher.Dispose();
                _startWatcher = null;
            }

            if (_stopWatcher != null)
            {
                _stopWatcher.EventArrived -= OnProcessStopped;
                _stopWatcher.Stop();
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
        if (!_isDisposed.CompareAndSet(false, true))
            return;

        DisposeWatchers();
        /* Ignore exceptions during dispose */
    }
}
