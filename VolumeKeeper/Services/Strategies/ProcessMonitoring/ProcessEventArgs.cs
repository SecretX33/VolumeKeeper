using System;

namespace VolumeKeeper.Services.Strategies.ProcessMonitoring;

public class ProcessEventArgs : EventArgs
{
    public int Id { get; init; }
    public string ExecutableName { get; init; } = string.Empty;
}
