using System;

namespace VolumeKeeper.Services.Strategies.ProcessMonitoring;

public interface IProcessMonitorStrategy : IDisposable
{
    event EventHandler<ProcessEventArgs>? ProcessStarted;
    event EventHandler<ProcessEventArgs>? ProcessStopped;
    
    bool Initialize();
    void Start();
    void Stop();
    string Name { get; }
}