using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using VolumeKeeper.Util;

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
    private TraceEventSession? _session;
    private ETWTraceEventSource? _source;
    private Task? _processingTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private volatile bool _isRunning;
    private readonly AtomicReference<bool> _isDisposed = new(false);

    public event EventHandler<ProcessEventArgs>? ProcessStarted;
    public event EventHandler<ProcessEventArgs>? ProcessStopped;

    public string Name => "ETW (Event Tracing for Windows)";

    public bool Initialize()
    {
        try
        {
            if (!TraceEventSession.IsElevated() ?? false)
            {
                App.Logger.LogDebug("ETW strategy requires administrator privileges", "EtwProcessMonitorStrategy");
                return false;
            }

            _session = new TraceEventSession("VolumeKeeperETWSession");

            _session.EnableKernelProvider(
                KernelTraceEventParser.Keywords.Process |
                KernelTraceEventParser.Keywords.ImageLoad
            );

            App.Logger.LogDebug("ETW process monitor initialized successfully", "EtwProcessMonitorStrategy");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            App.Logger.LogInfo("ETW strategy initialization failed: Administrator privileges required", "EtwProcessMonitorStrategy");
            DisposeSession();
            return false;
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to initialize ETW process monitor", ex, "EtwProcessMonitorStrategy");
            DisposeSession();
            return false;
        }
    }

    public void Start()
    {
        if (_isRunning || _session == null) return;

        _isRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();

        _source = new ETWTraceEventSource(_session.SessionName);

        _source.Kernel.ProcessStart += OnProcessStart;
        _source.Kernel.ProcessStop += OnProcessStop;

        _processingTask = Task.Run(() =>
        {
            try
            {
                _source.Process();
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

    private void OnProcessStart(ProcessTraceData data)
    {
        try
        {
            App.Logger.LogDebug($"[PROCESS START] {DateTime.Now:HH:mm:ss.fff}");
            App.Logger.LogDebug($"  PID: {data.ProcessID}");
            App.Logger.LogDebug($"  Process Name: {data.ProcessName}");
            App.Logger.LogDebug($"  Image Name: {data.ImageFileName}");
            App.Logger.LogDebug($"  Command Line: {data.CommandLine}");
            App.Logger.LogDebug($"  Session ID: {data.SessionID}");
            App.Logger.LogDebug($"  Exit Status: {data.ExitStatus}");

            var processName = GetProcessName(data);
            if (!string.IsNullOrEmpty(processName))
            {
                ProcessStarted?.Invoke(this, new ProcessEventArgs
                {
                    ProcessName = processName,
                    ProcessId = data.ProcessID
                });
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError($"Error handling ETW process start event for PID {data.ProcessID}", ex, "EtwProcessMonitorStrategy");
        }
    }

    private void OnProcessStop(ProcessTraceData data)
    {
        try
        {
            var processName = GetProcessName(data);
            if (!string.IsNullOrEmpty(processName))
            {
                ProcessStopped?.Invoke(this, new ProcessEventArgs
                {
                    ProcessName = processName,
                    ProcessId = data.ProcessID
                });
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError($"Error handling ETW process stop event for PID {data.ProcessID}", ex, "EtwProcessMonitorStrategy");
        }
    }

    private string GetProcessName(ProcessTraceData data)
    {
        try
        {
            if (!string.IsNullOrEmpty(data.ImageFileName))
            {
                return Path.GetFileName(data.ImageFileName);
            }

            if (!string.IsNullOrEmpty(data.ProcessName))
            {
                return data.ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? data.ProcessName
                    : $"{data.ProcessName}.exe";
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;

        try
        {
            _cancellationTokenSource?.Cancel();
            _source?.StopProcessing();

            if (_processingTask != null && !_processingTask.IsCompleted)
            {
                _processingTask.Wait(TimeSpan.FromSeconds(2));
            }
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
            _session?.Dispose();
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Error disposing ETW session", ex, "EtwProcessMonitorStrategy");
        }
        finally
        {
            _session = null;
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
            _source?.Dispose();
            DisposeSession();
        }
        catch
        {
            /* Ignore exceptions during dispose */
        }
        GC.SuppressFinalize(this);
    }
}
