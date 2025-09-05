using System;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace VolumeKeeper.Models;

public class VolumeSettings
{
    [JsonPropertyName("volumes")]
    public ConcurrentDictionary<string, int> ApplicationVolumes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.Now;

    [JsonPropertyName("autoRestoreEnabled")]
    public bool AutoRestoreEnabled { get; set; } = true;

    [JsonPropertyName("autoScrollLogsEnabled")]
    public bool AutoScrollLogsEnabled { get; set; } = true;

    [JsonPropertyName("lastVolumeBeforeMute")]
    public ConcurrentDictionary<string, int> LastVolumeBeforeMute { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void SetVolume(string executableName, int volumePercentage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);

        if (volumePercentage < 0 || volumePercentage > 100)
            throw new ArgumentOutOfRangeException(nameof(volumePercentage), "Volume must be between 0 and 100");

        ApplicationVolumes[executableName.ToLowerInvariant()] = volumePercentage;
        LastUpdated = DateTime.Now;
    }

    public int? GetVolume(string executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
            return null;

        return ApplicationVolumes.TryGetValue(executableName.ToLowerInvariant(), out var volume)
            ? volume
            : null;
    }

    public void RemoveVolume(string executableName)
    {
        if (!string.IsNullOrWhiteSpace(executableName))
        {
            ApplicationVolumes.TryRemove(executableName.ToLowerInvariant(), out _);
            LastUpdated = DateTime.Now;
        }
    }
}
