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

public record NamedVolumeApplicationId(string Name) : VolumeApplicationId(ApplicationNameMatchType.Name);

public record PathVolumeApplicationId(string Path) : VolumeApplicationId(ApplicationNameMatchType.Path)
{
    public override IEnumerable<VolumeApplicationId> GetAllVariants()
    {
        var variants = new List<VolumeApplicationId> { this };
        var fileName = System.IO.Path.GetFileName(Path).ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(fileName) && !string.Equals(fileName, Path, StringComparison.OrdinalIgnoreCase))
        {
            variants.Add(new NamedVolumeApplicationId(fileName));
        }
        return variants;
    }
}

public enum ApplicationNameMatchType
{
    Name,
    Path,
}
