using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using VolumeKeeper.Models.Log;

namespace VolumeKeeper.Services.Log;

public abstract class LoggingService : IDisposable
{
    public static ObservableCollection<LogEntry> LogEntries { get; } = [];
    protected string? DefaultSource { get; }

    protected LoggingService(string? defaultSource = null, [CallerFilePath] string callerFilePath = "")
    {
        DefaultSource = defaultSource ?? InferSource(callerFilePath);
    }

    public void Log(
        LogLevel level,
        string message,
        string? source,
        Exception? exception = null
    ) => LogInternal(
        level: level,
        message: message,
        source: source ?? DefaultSource ?? string.Empty,
        exception: exception
    );

    protected abstract void LogInternal(
        LogLevel level,
        string message,
        string source,
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

    public abstract LoggingService Named(string? source = null, [CallerFilePath] string callerFilePath = "");

    protected string? InferSource(string callerFilePath)
    {
        try
        {
            var stackTrace = new StackTrace(true);
            var frames = stackTrace.GetFrames();

            // Skip frames from NamedLoggingService itself
            foreach (var frame in frames.Skip(1))
            {
                var method = frame.GetMethod();
                if (method?.DeclaringType == null ||
                    !method.DeclaringType.FullName?.Contains("LoggingService") != true) continue;

                var className = method.DeclaringType.FullName ?? method.DeclaringType.Name;
                if (className.Contains('+')) // Nested class
                {
                    className = className.Substring(0, className.IndexOf('+'));
                }
                if (className.Contains('.')) // Namespace present
                {
                    className = className.Substring(className.LastIndexOf('.') + 1);
                }

                if (!method.DeclaringType.IsGenericType) return className;

                var genericTypeName = method.DeclaringType.GetGenericTypeDefinition().Name;
                className = genericTypeName.Contains('`')
                    ? genericTypeName.Substring(0, genericTypeName.IndexOf('`'))
                    : genericTypeName;
                return className;
            }

            // Fallback to file name
            if (!string.IsNullOrEmpty(callerFilePath))
            {
                return Path.GetFileNameWithoutExtension(callerFilePath);
            }
        }
        catch (Exception)
        {
            // Ignore any errors in source inference
        }

        return null;
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
