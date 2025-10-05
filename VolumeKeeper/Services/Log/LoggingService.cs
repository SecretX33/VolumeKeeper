using System;
using System.Collections.ObjectModel;
using VolumeKeeper.Models.Log;

namespace VolumeKeeper.Services.Log;

public abstract class LoggingService : IDisposable
{
    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    protected abstract void Log(
        LogLevel level,
        string message,
        string? source,
        Exception? exception = null
    );

    public void LogDebug(string message, string? source = null) => LogDebug(message, null, source);
    public void LogInfo(string message, string? source = null) => LogInfo(message, null, source);
    public void LogWarning(string message, string? source = null) => LogWarning(message, null, source);
    public void LogError(string message, string? source = null) => LogError(message, null, source);

    public void LogDebug(string message, Exception? exception, string? source = null) => Log(LogLevel.Debug, message, source, exception);
    public void LogInfo(string message, Exception? exception, string? source = null) => Log(LogLevel.Info, message, source, exception);
    public void LogWarning(string message, Exception? exception, string? source = null) => Log(LogLevel.Warning, message, source, exception);
    public void LogError(string message, Exception? exception, string? source = null) => Log(LogLevel.Error, message, source, exception);

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
