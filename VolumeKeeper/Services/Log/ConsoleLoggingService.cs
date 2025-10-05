using System;
using System.Runtime.CompilerServices;
using VolumeKeeper.Models.Log;

namespace VolumeKeeper.Services.Log;

/**
* <p>A simple logging service that outputs logs to the console.</p>
*
* <p>It doesn't store log entries anywhere because it's meant to be used in scenarios where a more
* appropriate logging service cannot be used.</p>
*/
// ReSharper disable ExplicitCallerInfoArgument
public partial class ConsoleLoggingService(
    string? defaultSource = null,
    [CallerFilePath] string callerFilePath = ""
) : LoggingService(defaultSource, callerFilePath)
{
    protected override void LogInternal(
        LogLevel level,
        string message,
        string source,
        Exception? exception = null
    ) {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var levelStr = level.ToString().ToUpper().PadRight(7);

        var logMessage = $"[{timestamp}] [{levelStr}] [{source}] {message}";

        if (exception != null)
        {
            logMessage += $"\n  Exception: {exception.GetType().Name}: {exception.Message}";
            if (exception.StackTrace != null)
            {
                logMessage += $"\n{exception.StackTrace}";
            }
        }

        Console.WriteLine(logMessage);
    }

    public override LoggingService Named(string? source = null, [CallerFilePath] string callerFilePath = "") =>
        new ConsoleLoggingService(source, callerFilePath);
}
