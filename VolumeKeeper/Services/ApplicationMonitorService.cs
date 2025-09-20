namespace VolumeKeeper.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using Managers;
using Strategies.ProcessMonitoring;
using Util;
using static VolumeKeeper.Util.Util;

public partial class ApplicationMonitorService(AudioSessionManager _audioSessionManager) : IDisposable
{
    private readonly AtomicReference<bool> _isDisposed = new(false);
    private IProcessMonitorStrategy? _activeStrategy;

    public void Initialize()
    {
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
            App.Logger.LogInfo($"Application launched: {e.ExecutableName} (PID: {e.Id})", "ApplicationMonitorService");
            var processInfo = GetProcessInfoOrNull(e.Id);
            if (processInfo == null) return;
            _audioSessionManager.OnProcessStarted(processInfo);
        }
        catch (Exception ex)
        {
            App.Logger.LogError($"Error handling process start event for {e.ExecutableName} (PID: {e.Id})", ex, "ApplicationMonitorService");
        }
    }

    private void OnProcessStopped(object? sender, ProcessEventArgs e)
    {
        try
        {
            App.Logger.LogInfo($"Application stopped: {e.ExecutableName} (PID: {e.Id})", "ApplicationMonitorService");
            _audioSessionManager.OnProcessStopped(e.Id);
        }
        catch (Exception ex)
        {
            App.Logger.LogError($"Error handling process stop event for {e.ExecutableName} (PID: {e.Id})", ex, "ApplicationMonitorService");
        }
    }

    public void Dispose()
    {
        if (!_isDisposed.CompareAndSet(false, true))
            return;

        try
        {
            if (_activeStrategy != null)
            {
                _activeStrategy.ProcessStarted -= OnProcessStarted;
                _activeStrategy.ProcessStopped -= OnProcessStopped;
                _activeStrategy.Stop();
                _activeStrategy.Dispose();
            }
        }
        catch
        {
            /* Ignore exceptions during dispose */
        }
        GC.SuppressFinalize(this);
    }
}
