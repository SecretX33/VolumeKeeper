using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using VolumeKeeper.Models.Log;

namespace VolumeKeeper.Services.Log;

internal sealed partial class NamedLogger : Logger
{
    public Logger Delegate { get; }
    private readonly string? _defaultSource;

    internal NamedLogger(Logger loggerDelegate, string? source, string filePath)
    {
        Delegate = loggerDelegate;
        _defaultSource = source ?? InferSource(filePath);
    }

    public override void Log(LogLevel level, string message, string? source, Exception? exception = null) =>
        Delegate.Log(
            level: level,
            message: message,
            source: source ?? _defaultSource,
            exception: exception
        );

    private string? InferSource(string filePath)
    {
        try
        {
            var stackTrace = new StackTrace(true);
            var frames = stackTrace.GetFrames();

            // Skip frames from NamedLogger itself
            foreach (var frame in frames.Skip(1))
            {
                var method = frame.GetMethod();
                if (method?.DeclaringType == null ||
                    !method.DeclaringType.FullName?.Contains("Logger") != true) continue;

                var className = method.DeclaringType.FullName ?? method.DeclaringType.Name;
                if (className.Contains('+')) // Nested class, e.g. 'VolumeKeeper.App+<OnLaunched>d__21' -> 'VolumeKeeper.App'
                {
                    className = className.Substring(0, className.IndexOf('+'));
                }
                if (className.Contains('.')) // Namespace present, 'VolumeKeeper.App' -> 'App'
                {
                    className = className.Substring(className.LastIndexOf('.') + 1);
                }

                if (!method.DeclaringType.IsGenericType) return className;

                className = method.DeclaringType.GetGenericTypeDefinition().Name;
                if (className.Contains('`'))
                {
                    className = className.Substring(0, className.IndexOf('`'));
                }

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
