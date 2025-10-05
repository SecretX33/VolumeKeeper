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
    public LoggingService Delegate { get; }
    private readonly string? _defaultSource;

    public NamedLoggingService(LoggingService loggerDelegate, string? source, string filePath)
    {
        Delegate = loggerDelegate;
        _defaultSource = source ?? InferSource(filePath);
    }

    public override void Log(LogLevel level, string message, string? source, Exception? exception = null)
    {
        source ??= _defaultSource;
        Delegate.Log(level, message, source, exception);
    }

    private string? InferSource(string filePath)
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
            if (!string.IsNullOrEmpty(filePath))
            {
                return Path.GetFileNameWithoutExtension(filePath);
            }
        }
        catch (Exception)
        {
            // Ignore any errors in source inference
        }

        return null;
    }

    public override void Dispose()
    {
        try
        {
            Delegate.Dispose();
        }
        finally
        {
            base.Dispose();
        }
    }
}
