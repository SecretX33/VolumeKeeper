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

    public void Debug(string message, string? source = null) => Debug(message, null, source);
    public void Info(string message, string? source = null) => Info(message, null, source);
    public void Warning(string message, string? source = null) => Warning(message, null, source);
    public void Error(string message, string? source = null) => Error(message, null, source);

    public void Debug(string message, Exception? exception, string? source = null) => Log(LogLevel.Debug, message, source, exception);
    public void Info(string message, Exception? exception, string? source = null) => Log(LogLevel.Info, message, source, exception);
    public void Warning(string message, Exception? exception, string? source = null) => Log(LogLevel.Warning, message, source, exception);
    public void Error(string message, Exception? exception, string? source = null) => Log(LogLevel.Error, message, source, exception);

    /**
     * Creates a named logging service that prefixes all log entries with the specified source name,
     * or infers it from the caller's data.
     */
    public LoggingService Named(string? source = null, [CallerFilePath] string filePath = "")
    {
        var resolvedLoggingService = this;
        while (resolvedLoggingService is NamedLoggingService service)
        {
            resolvedLoggingService = service.Delegate;
        }
        return new NamedLoggingService(loggerDelegate: resolvedLoggingService, source: source, filePath: filePath);
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
