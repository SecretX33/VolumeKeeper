using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using VolumeKeeper.Util.Converter;

namespace VolumeKeeper.Models;

public sealed record VolumeSettings {
    public IReadOnlyCollection<ApplicationVolumeConfig> ApplicationVolumes { get; init; } = ReadOnlyCollection<ApplicationVolumeConfig>.Empty;
    public bool AutoRestoreEnabled { get; init; } = true;
    public bool AutoScrollLogsEnabled { get; init; } = true;
    public DateTime LastUpdated { get; init; } = DateTime.Now;
}

public sealed record ApplicationVolumeConfig(
    [property: JsonConverter(typeof(ApplicationIdJsonConverter))]
    VolumeApplicationId Id,
    int? Volume
);

public sealed class VolumeApplicationId {
    public readonly string Path;
    private int? _cachedHashCode;
    public string Name { get; }

    public VolumeApplicationId(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }
        Path = path;
        Name = System.IO.Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(Name)) {
            throw new ArgumentException("Path must contain a valid file name.", nameof(path));
        }
    }

    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return string.Equals(Path, ((VolumeApplicationId)obj).Path, StringComparison.OrdinalIgnoreCase);
    }

    // ReSharper disable NonReadonlyMemberInGetHashCode
    public override int GetHashCode()
    {
        if (!_cachedHashCode.HasValue)
        {
            _cachedHashCode = Path.ToLowerInvariant().GetHashCode();
        }
        return _cachedHashCode.Value;
    }

    public override string ToString() => $"{Name} ({Path})";
}
