using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace VolumeKeeper.Models;

public record VolumeSettings {
    public IReadOnlyCollection<ApplicationVolumeConfig> ApplicationVolumes { get; init; } = ReadOnlyCollection<ApplicationVolumeConfig>.Empty;
    public bool AutoRestoreEnabled { get; init; } = true;
    public bool AutoScrollLogsEnabled { get; init; } = true;
    public DateTime LastUpdated { get; init; } = DateTime.Now;
}

public record ApplicationVolumeConfig(
    string Name,
    ApplicationNameMatchType NameMatchType,
    int Volume,
    int? LastVolumeBeforeMute = null
);

public enum ApplicationNameMatchType
{
    Name,
    Path,
}
