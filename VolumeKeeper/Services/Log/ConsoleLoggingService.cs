using System;
using VolumeKeeper.Models.Log;

namespace VolumeKeeper.Services.Log;

/**
 * <p>A simple logging service that outputs logs to the console.</p>
 *
 * <p>It doesn't store log entries anywhere because it's meant to be used in scenarios where a more
 * appropriate logging service cannot be used.</p>
 */
public partial class ConsoleLoggingService : LoggingService
{
    protected override void Log(
        LogLevel level,
        string message,
        string? source,
        Exception? exception = null
    )
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var levelStr = level.ToString().ToUpper().PadRight(7);
        var sourceStr = source ?? string.Empty;

        var logMessage = $"[{timestamp}] [{levelStr}] [{sourceStr}] {message}";

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
}
