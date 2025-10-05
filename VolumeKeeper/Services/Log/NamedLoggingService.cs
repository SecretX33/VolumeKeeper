using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using VolumeKeeper.Models.Log;

namespace VolumeKeeper.Services.Log;

public sealed partial class NamedLoggingService : LoggingService
{
    private readonly LoggingService _delegate;
    private readonly string? _defaultSource;
    public new ObservableCollection<LogEntry> LogEntries => _delegate.LogEntries;

    public NamedLoggingService(LoggingService loggerDelegate, string? source = null)
    {
        _delegate = loggerDelegate;
        _defaultSource = source ?? InferSource();
    }

    public override void Log(LogLevel level, string message, string? source, Exception? exception = null)
    {
        source ??= _defaultSource;
        _delegate.Log(level, message, source, exception);
    }

    private string? InferSource([CallerFilePath] string filePath = "")
    {
        var stackTrace = new StackTrace(true);
        var frames = stackTrace.GetFrames();

        // Skip frames from NamedLoggingService itself
        foreach (var frame in frames.Skip(1))
        {
            var method = frame.GetMethod();
            if (method?.DeclaringType == null ||
                !method.DeclaringType.FullName?.Contains("LoggingService") != true) continue;

            var className = method.DeclaringType.Name;
            if (!method.DeclaringType.IsGenericType) return className;

            var genericTypeName = method.DeclaringType.GetGenericTypeDefinition().Name;
            className = genericTypeName.Contains('`')
                ? genericTypeName.Substring(0, genericTypeName.IndexOf('`'))
                : genericTypeName;
            return className;
        }

        // Fallback to file name
        if (!string.IsNullOrEmpty(filePath))
        {
            return Path.GetFileNameWithoutExtension(filePath);
        }

        return null;
    }

    public override void Dispose()
    {
        try
        {
            _delegate.Dispose();
        }
        finally
        {
            base.Dispose();
        }
    }
}
