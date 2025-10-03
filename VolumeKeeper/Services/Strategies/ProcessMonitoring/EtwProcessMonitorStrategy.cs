using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using VolumeKeeper.Util;
using static VolumeKeeper.Util.Util;

namespace VolumeKeeper.Services.Strategies.ProcessMonitoring;

/**
 * <p>EtwProcessMonitorStrategy uses Event Tracing for Windows (ETW) to monitor process start and stop events.
 * ETW is a kernel-level, high-performance logging system built into Windows that provides direct access to kernel events.</p>
 *
 * <p><b>Performance:</b> Very fast with minimal latency and low overhead. Designed for real-time diagnostics at scale
 * (used by professional tools like Sysmon, Process Monitor, and PerfView).</p>
 *
 * <p><b>Reliability:</b> Kernel sends events directly, ensuring no missed events even for short-lived processes.
 * Captures rich process data including process name, PID, parent PID, command line, session ID, and user SID.</p>
 *
 * <p><b>Note:</b> This strategy requires administrator privileges to create ETW sessions and subscribe to kernel events.
 * It is the preferred strategy when available due to its superior performance and reliability.</p>
 */
public partial class EtwProcessMonitorStrategy : IProcessMonitorStrategy
{
    private TraceEventSession? _eventSession;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly AtomicReference<bool> _isRunning = new(false);
    private readonly AtomicReference<bool> _isDisposed = new(false);

    public event EventHandler<ProcessEventArgs>? ProcessStarted;
    public event EventHandler<ProcessEventArgs>? ProcessStopped;

    public string Name => "ETW (Event Tracing for Windows)";

    public bool Initialize()
    {
        try
        {
            if (!IsElevated())
            {
                App.Logger.LogDebug("ETW strategy requires administrator privileges", "EtwProcessMonitorStrategy");
                return false;
            }

            _eventSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName);
            _eventSession.EnableKernelProvider(KernelTraceEventParser.Keywords.Process);

            App.Logger.LogDebug("ETW process monitor initialized successfully", "EtwProcessMonitorStrategy");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            App.Logger.LogInfo("ETW strategy initialization failed: Administrator privileges required", "EtwProcessMonitorStrategy");
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to initialize ETW process monitor", ex, "EtwProcessMonitorStrategy");
        }

        DisposeSession();
        return false;
    }

    public void Start()
    {
        if (_eventSession == null || !_isRunning.CompareAndSet(false, true)) return;

        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }
        _cancellationTokenSource = new CancellationTokenSource();

        var eventSource = _eventSession.Source;
        eventSource.Kernel.ProcessStart += EventListener_OnProcessStart;
        eventSource.Kernel.ProcessStop += EventListener_OnProcessStop;

        Task.Run(() =>
        {
            try
            {
                eventSource.Process();
            }
            catch (Exception ex)
            {
                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    App.Logger.LogError("Error processing ETW events", ex, "EtwProcessMonitorStrategy");
                }
            }
        }, _cancellationTokenSource.Token);

        App.Logger.LogDebug("ETW process monitor started", "EtwProcessMonitorStrategy");
    }

    private void EventListener_OnProcessStart(ProcessTraceData data)
    {
        try
        {
            HandleEvent(ProcessStarted, data);
        }
        catch (Exception ex)
        {
            App.Logger.LogError($"Error handling ETW process start event for PID {data.ProcessID}", ex, "EtwProcessMonitorStrategy");
        }
    }

    private void EventListener_OnProcessStop(ProcessTraceData data)
    {
        try
        {
            HandleEvent(ProcessStopped, data);
        }
        catch (Exception ex)
        {
            App.Logger.LogError($"Error handling ETW process stop event for PID {data.ProcessID}", ex, "EtwProcessMonitorStrategy");
        }
    }

    private void HandleEvent(EventHandler<ProcessEventArgs>? eventHandler, ProcessTraceData data) =>
        eventHandler?.Invoke(this, new ProcessEventArgs
        {
            ExecutableName = GetProcessName(data),
            Id = data.ProcessID,
        });

    private string GetProcessName(ProcessTraceData data)
    {
        try
        {
            if (!string.IsNullOrEmpty(data.ImageFileName)) return Path.GetFileName(data.ImageFileName);
            if (!string.IsNullOrEmpty(data.ProcessName)) return data.ProcessName;
            return string.Empty;
        }
        catch (Exception ex)
        {
            App.Logger.LogWarning($"Error retrieving process name for PID {data.ProcessID}", ex, "EtwProcessMonitorStrategy");
            return string.Empty;
        }
    }

    public void Stop()
    {
        if (!_isRunning.CompareAndSet(true, false)) return;

        try
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
            _eventSession?.Stop();
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Error stopping ETW process monitor", ex, "EtwProcessMonitorStrategy");
        }

        App.Logger.LogDebug("ETW process monitor stopped", "EtwProcessMonitorStrategy");
    }

    private void DisposeSession()
    {
        try
        {
            _eventSession?.Stop();
            _eventSession?.Dispose();
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Error disposing ETW session", ex, "EtwProcessMonitorStrategy");
        }
        finally
        {
            _eventSession = null;
        }
    }

    public void Dispose()
    {
        if (!_isDisposed.CompareAndSet(false, true))
            return;

        try
        {
            Stop();
            _cancellationTokenSource?.Dispose();
            DisposeSession();
        }
        catch
        {
            /* Ignore exceptions during dispose */
        }
        GC.SuppressFinalize(this);
    }
}
