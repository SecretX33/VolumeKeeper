using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace VolumeKeeper.Models;

public record VolumeSettings {
    public IReadOnlyDictionary<string, int> ApplicationVolumes { get; init; } = ReadOnlyDictionary<string, int>.Empty;
    public bool AutoRestoreEnabled { get; init; } = true;
    public bool AutoScrollLogsEnabled { get; init; } = true;
    public IReadOnlyDictionary<string, int> LastVolumeBeforeMute { get; init; } = ReadOnlyDictionary<string, int>.Empty;
    public DateTime LastUpdated { get; init; } = DateTime.Now;
}
