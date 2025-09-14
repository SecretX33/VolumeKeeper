using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public virtual IEnumerable<VolumeApplicationId> GetAllVariants() => [this];
}

public record NamedVolumeApplicationId(string Name) : VolumeApplicationId(ApplicationNameMatchType.Name)
{
    public virtual bool Equals(NamedVolumeApplicationId? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return base.Equals(other) && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(base.GetHashCode());
        hashCode.Add(Name, StringComparer.OrdinalIgnoreCase);
        return hashCode.ToHashCode();
    }
}

public record PathVolumeApplicationId(string Path) : VolumeApplicationId(ApplicationNameMatchType.Path)
{
    public override IEnumerable<VolumeApplicationId> GetAllVariants()
    {
        var variants = new List<VolumeApplicationId> { this };
        var fileName = System.IO.Path.GetFileName(Path);

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            variants.Add(new NamedVolumeApplicationId(fileName));
        }
        return variants;
    }

    public virtual bool Equals(PathVolumeApplicationId? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return base.Equals(other) && string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(base.GetHashCode());
        hashCode.Add(Path, StringComparer.OrdinalIgnoreCase);
        return hashCode.ToHashCode();
    }
}

public enum ApplicationNameMatchType
{
    Name,
    Path,
}
