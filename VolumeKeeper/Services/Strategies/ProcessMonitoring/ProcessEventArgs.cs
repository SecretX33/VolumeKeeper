using System;

namespace VolumeKeeper.Services.Strategies.ProcessMonitoring;

public class ProcessEventArgs : EventArgs
{
    public string ProcessName { get; init; } = string.Empty;
    public int ProcessId { get; init; }
}