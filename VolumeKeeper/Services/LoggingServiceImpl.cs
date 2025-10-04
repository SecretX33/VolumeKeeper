using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using NLog;
using VolumeKeeper.Models.Log;
using VolumeKeeper.Util;

namespace VolumeKeeper.Services;

public abstract class LoggingService
{
    public ObservableCollection<LogEntry> LogEntries { get; } = [];
    public abstract Task FlushAsync();

    protected abstract void Log(
        Models.Log.LogLevel level,
        string message,
        string? source,
        Exception? exception = null
    );

    public void LogDebug(string message, string? source = null) => LogDebug(message, null, source);
    public void LogInfo(string message, string? source = null) => LogInfo(message, null, source);
    public void LogWarning(string message, string? source = null) => LogWarning(message, null, source);
    public void LogError(string message, string? source = null) => LogError(message, null, source);

    public void LogDebug(string message, Exception? exception, string? source = null) => Log(Models.Log.LogLevel.Debug, message, source, exception);
    public void LogInfo(string message, Exception? exception, string? source = null) => Log(Models.Log.LogLevel.Info, message, source, exception);
    public void LogWarning(string message, Exception? exception, string? source = null) => Log(Models.Log.LogLevel.Warning, message, source, exception);
    public void LogError(string message, Exception? exception, string? source = null) => Log(Models.Log.LogLevel.Error, message, source, exception);
}

public partial class LoggingServiceImpl : LoggingService, IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private const int MaxInMemoryEntries = 1000;
    private readonly AtomicReference<bool> _isDisposed = new(false);

    public LoggingServiceImpl(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;

        // Configure NLog to use our config file
        var configFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NLog.config");
        if (System.IO.File.Exists(configFile))
        {
            LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(configFile);
        }

        LogInfo("VolumeKeeper logging service started", "LoggingService");
    }

    protected override void Log(
        Models.Log.LogLevel level,
        string message,
        string? source,
        Exception? exception = null
    ) {
        if (_isDisposed.Get()) return;

        var details = exception != null
            ? $"{exception.GetType().Name}: {exception.Message}"
            : null;

        source ??= GetCallerSource();

        // Create log entry for UI
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Details = details,
            Source = source
        };

        // Update UI collection
        _dispatcherQueue.TryEnqueueImmediate(() =>
        {
            LogEntries.Insert(0, entry); // Insert at beginning for newest-first order

            while (LogEntries.Count > MaxInMemoryEntries)
            {
                LogEntries.RemoveAt(LogEntries.Count - 1); // Remove oldest (last) entry
            }
        });

        // Log to NLog with proper logger name (using source as logger name)
        var nlogLogger = LogManager.GetLogger(source);

        // Map our log level to NLog level and log with exception if present
        switch (level)
        {
            case Models.Log.LogLevel.Debug:
                if (exception != null)
                    nlogLogger.Debug(exception, message);
                else
                    nlogLogger.Debug(message);
                break;
            case Models.Log.LogLevel.Info:
                if (exception != null)
                    nlogLogger.Info(exception, message);
                else
                    nlogLogger.Info(message);
                break;
            case Models.Log.LogLevel.Warning:
                if (exception != null)
                    nlogLogger.Warn(exception, message);
                else
                    nlogLogger.Warn(message);
                break;
            case Models.Log.LogLevel.Error:
                if (exception != null)
                    nlogLogger.Error(exception, message);
                else
                    nlogLogger.Error(message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(level), level, null);
        }
    }

    private string GetCallerSource([CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "")
    {
        // Try to get a more meaningful source from the stack trace
        var stackTrace = new StackTrace(true);
        var frames = stackTrace.GetFrames();

        // Skip frames from LoggingService itself
        foreach (var frame in frames.Skip(2))
        {
            var method = frame.GetMethod();
            if (method?.DeclaringType != null &&
                !method.DeclaringType.FullName?.Contains("LoggingService") == true)
            {
                var className = method.DeclaringType.Name;
                if (method.DeclaringType.IsGenericType)
                {
                    var genericTypeName = method.DeclaringType.GetGenericTypeDefinition().Name;
                    className = genericTypeName.Contains('`')
                        ? genericTypeName.Substring(0, genericTypeName.IndexOf('`'))
                        : genericTypeName;
                }
                return className;
            }
        }

        // Fallback to file name without extension if available
        if (!string.IsNullOrEmpty(filePath))
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            return fileName;
        }

        return "Unknown";
    }

    public override async Task FlushAsync()
    {
        if (_isDisposed.Get()) return;

        // Flush NLog targets
        await Task.Run(() => LogManager.Flush());
    }

    public void Dispose()
    {
        if (!_isDisposed.CompareAndSet(false, true)) return;

        try
        {
            // Flush and shutdown NLog
            LogManager.Flush(TimeSpan.FromSeconds(1));
            LogManager.Shutdown();
        }
        catch
        {
            // Ignore exceptions during dispose
        }

        GC.SuppressFinalize(this);
    }
}
