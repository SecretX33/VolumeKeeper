using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using NLog;
using NLog.Config;
using VolumeKeeper.Models.Log;
using VolumeKeeper.Util;
using LogLevel = VolumeKeeper.Models.Log.LogLevel;

namespace VolumeKeeper.Services.Log;

public sealed partial class FileLoggingService : LoggingService
{
    private readonly DispatcherQueue _dispatcherQueue;
    private const int MaxInMemoryEntries = 1000;
    private readonly AtomicReference<bool> _isDisposed = new(false);

    public FileLoggingService(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;

        // Configure NLog to use our config file
        var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NLog.config");
        if (File.Exists(configFile))
        {
            LogManager.Configuration = new XmlLoggingConfiguration(configFile);
        }

        LogInfo("VolumeKeeper logging service started", "LoggingService");
    }

    public override void Log(
        LogLevel level,
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
            case LogLevel.Debug:
                if (exception != null)
                    nlogLogger.Debug(exception, message);
                else
                    nlogLogger.Debug(message);
                break;
            case LogLevel.Info:
                if (exception != null)
                    nlogLogger.Info(exception, message);
                else
                    nlogLogger.Info(message);
                break;
            case LogLevel.Warning:
                if (exception != null)
                    nlogLogger.Warn(exception, message);
                else
                    nlogLogger.Warn(message);
                break;
            case LogLevel.Error:
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
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            return fileName;
        }

        return "Unknown";
    }

    public override void Dispose()
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

        base.Dispose();
    }
}
