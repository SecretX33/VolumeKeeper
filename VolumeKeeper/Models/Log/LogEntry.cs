using System;
using Windows.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace VolumeKeeper.Models.Log;

public sealed record LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? Source { get; set; }

    public string FormattedTime => Timestamp.ToString("HH:mm:ss");
    public string FormattedDate => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

    // UI properties for LogsPage binding
    public Symbol IconSymbol => Level switch
    {
        LogLevel.Debug => Symbol.Repair,
        LogLevel.Info => Symbol.Important,
        LogLevel.Warning => Symbol.ReportHacked,
        LogLevel.Error => Symbol.Cancel,
        _ => Symbol.Important
    };

    public Brush TypeColor => GetColorForLevel(Level);

    private static Brush GetColorForLevel(LogLevel level)
    {
        var color = level switch
        {
            LogLevel.Debug => Color.FromArgb(255, 128, 128, 128),
            LogLevel.Info => Color.FromArgb(255, 0, 120, 212),
            LogLevel.Warning => Color.FromArgb(255, 255, 185, 0),
            LogLevel.Error => Color.FromArgb(255, 232, 17, 35),
            _ => Color.FromArgb(255, 118, 118, 118)
        };
        return new SolidColorBrush(color);
    }
}
