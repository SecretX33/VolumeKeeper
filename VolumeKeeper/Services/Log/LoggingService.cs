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

    public void Debug(string message, string? source = null) => Debug(message, null, source);
    public void Info(string message, string? source = null) => Info(message, null, source);
    public void Warning(string message, string? source = null) => Warning(message, null, source);
    public void Error(string message, string? source = null) => Error(message, null, source);

    public void Debug(string message, Exception? exception, string? source = null) => Log(LogLevel.Debug, message, source, exception);
    public void Info(string message, Exception? exception, string? source = null) => Log(LogLevel.Info, message, source, exception);
    public void Warning(string message, Exception? exception, string? source = null) => Log(LogLevel.Warning, message, source, exception);
    public void Error(string message, Exception? exception, string? source = null) => Log(LogLevel.Error, message, source, exception);

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
