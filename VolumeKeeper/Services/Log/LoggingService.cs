using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using VolumeKeeper.Models.Log;

namespace VolumeKeeper.Services.Log;

public abstract class LoggingService : IDisposable
{
    public static ObservableCollection<LogEntry> LogEntries { get; } = [];

    public abstract void Log(
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

    public LoggingService Named(string? source = null, [CallerFilePath] string filePath = "")
    {
        var resolvedLoggingService = this is NamedLoggingService ? ((NamedLoggingService)this).Delegate : this;
        return new NamedLoggingService(loggerDelegate: resolvedLoggingService, source: source, filePath: filePath);
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
