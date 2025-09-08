using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using VolumeKeeper.Models.Log;

namespace VolumeKeeper.Services;

public interface ILoggingService
{
    ObservableCollection<LogEntry> LogEntries { get; }
    void LogDebug(string message, string? source = null);
    void LogInfo(string message, string? source = null);
    void LogWarning(string message, string? source = null);
    void LogError(string message, Exception? exception = null, string? source = null);
    Task FlushAsync();
}

public class LoggingService : ILoggingService, IDisposable
{
    private readonly ObservableCollection<LogEntry> _logEntries;
    private readonly Queue<LogEntry> _pendingFileWrites;
    private readonly SemaphoreSlim _fileWriteSemaphore;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly string _logFilePath;
    private readonly Timer _flushTimer;
    private const int MaxInMemoryEntries = 1000;
    private const int FileWriteBatchSize = 50;
    private volatile bool _disposed;

    public ObservableCollection<LogEntry> LogEntries => _logEntries;

    public LoggingService(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        _logEntries = new ObservableCollection<LogEntry>();
        _pendingFileWrites = new Queue<LogEntry>();
        _fileWriteSemaphore = new SemaphoreSlim(1, 1);

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VolumeKeeper",
            "logs"
        );

        Directory.CreateDirectory(logDirectory);
        _logFilePath = Path.Combine(logDirectory, $"volumekeeper_{DateTime.Now:yyyyMMdd}.log");

        _flushTimer = new Timer(async _ => await FlushAsync(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        LogInfo("VolumeKeeper logging service started", "LoggingService");
    }

    public void LogDebug(string message, string? source = null)
    {
        Log(LogLevel.Debug, message, null, source);
    }

    public void LogInfo(string message, string? source = null)
    {
        Log(LogLevel.Info, message, null, source);
    }

    public void LogWarning(string message, string? source = null)
    {
        Log(LogLevel.Warning, message, null, source);
    }

    public void LogError(string message, Exception? exception = null, string? source = null)
    {
        var details = exception != null
            ? $"{exception.GetType().Name}: {exception.Message}"
            : null;

        Log(LogLevel.Error, message, details, source);
    }

    private void Log(LogLevel level, string message, string? details, string? source)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Details = details,
            Source = source ?? GetCallerSource()
        };
        LogToConsole(entry);

        _dispatcherQueue.TryEnqueue(() =>
        {
            _logEntries.Insert(0, entry); // Insert at beginning for newest-first order

            while (_logEntries.Count > MaxInMemoryEntries)
            {
                _logEntries.RemoveAt(_logEntries.Count - 1); // Remove oldest (last) entry
            }
        });

        lock (_pendingFileWrites)
        {
            _pendingFileWrites.Enqueue(entry);

            if (_pendingFileWrites.Count >= FileWriteBatchSize)
            {
                _ = Task.Run(FlushAsync);
            }
        }
    }

    private static void LogToConsole(LogEntry entry)
    {
        Console.Out.WriteLine("{0} [{1}] [{2}] {3}{4}",
            [
                entry.FormattedDate,
                entry.Level,
                entry.Source ?? "Unknown",
                entry.Message,
                entry.Details != null ? $" | Details: {entry.Details}" : "",
            ]
        );
    }

    public async Task FlushAsync()
    {
        if (_disposed) return;

        List<LogEntry> entriesToWrite;
        lock (_pendingFileWrites)
        {
            if (_pendingFileWrites.Count == 0) return;
            entriesToWrite = _pendingFileWrites.ToList();
            _pendingFileWrites.Clear();
        }

        await _fileWriteSemaphore.WaitAsync();
        try
        {
            var lines = entriesToWrite.Select(entry =>
                $"{entry.FormattedDate} [{entry.Level,-7}] [{entry.Source ?? "Unknown",-20}] {entry.Message}" +
                (entry.Details != null ? $" | Details: {entry.Details}" : ""));

            await File.AppendAllLinesAsync(_logFilePath, lines);
        }
        catch
        {
            // If file logging fails, we don't want to crash the app
        }
        finally
        {
            _fileWriteSemaphore.Release();
        }
    }

    private string GetCallerSource()
    {
        var stackTrace = Environment.StackTrace;
        var lines = stackTrace.Split('\n');

        // Find the first line that's not from the logging service
        foreach (var line in lines.Skip(1))
        {
            if (!line.Contains("LoggingService") && !line.Contains("Log("))
            {
                var methodStart = line.IndexOf(" at ", StringComparison.Ordinal) + 4;
                if (methodStart > 3)
                {
                    var methodEnd = line.IndexOf('(', methodStart);
                    if (methodEnd > methodStart)
                    {
                        var fullMethod = line.Substring(methodStart, methodEnd - methodStart);
                        var lastDot = fullMethod.LastIndexOf('.');
                        return lastDot > 0 ? fullMethod.Substring(0, lastDot) : fullMethod;
                    }
                }
                break;
            }
        }

        return "Unknown";
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _flushTimer.Dispose();
        FlushAsync().Wait(TimeSpan.FromSeconds(1));
        _fileWriteSemaphore.Dispose();

        LogInfo("VolumeKeeper logging service stopped", "LoggingService");
    }
}
