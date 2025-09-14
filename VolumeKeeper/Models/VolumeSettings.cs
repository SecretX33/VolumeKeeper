using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using VolumeKeeper.Util.Converter;

namespace VolumeKeeper.Models;

public record VolumeSettings {
    public IReadOnlyCollection<ApplicationVolumeConfig> ApplicationVolumes { get; init; } = ReadOnlyCollection<ApplicationVolumeConfig>.Empty;
    public bool AutoRestoreEnabled { get; init; } = true;
    public bool AutoScrollLogsEnabled { get; init; } = true;
    public DateTime LastUpdated { get; init; } = DateTime.Now;
}

public record ApplicationVolumeConfig(
    [property: JsonConverter(typeof(ApplicationIdJsonConverter))]
    VolumeApplicationId Id,
    int? Volume,
    int? LastVolumeBeforeMute = null
);

public abstract record VolumeApplicationId(ApplicationNameMatchType NameMatchType)
{
    public abstract string Name { get; init; }

    public static VolumeApplicationId Create(string? executablePath, string executableName)
    {
        return !string.IsNullOrWhiteSpace(executablePath)
            ? new PathVolumeApplicationId(executablePath)
            : new NamedVolumeApplicationId(executableName);
    }

    public virtual bool Equals(VolumeApplicationId? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        // Both are path-based, compare paths
        if (this is PathVolumeApplicationId thisId && other is PathVolumeApplicationId otherId)
        {
            return string.Equals(thisId.Path, otherId.Path, StringComparison.OrdinalIgnoreCase);
        }

        // Fallback to name match if either is name match
        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Name, StringComparer.OrdinalIgnoreCase);
        return hashCode.ToHashCode();
    }
}

public sealed record NamedVolumeApplicationId : VolumeApplicationId
{
    public override string Name { get; init; }

    public NamedVolumeApplicationId(string name) : base(ApplicationNameMatchType.Name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be null or empty.", nameof(name));
        }
        Name = System.IO.Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(Name)) {
            throw new ArgumentException("Name must contain a valid file name.", nameof(name));
        }
    }
}

public sealed record PathVolumeApplicationId : VolumeApplicationId
{
    public readonly string Path;
    public override string Name { get; init; }

    public PathVolumeApplicationId(string path) : base(ApplicationNameMatchType.Path)
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
}

public enum ApplicationNameMatchType
{
    Name,
    Path,
}
