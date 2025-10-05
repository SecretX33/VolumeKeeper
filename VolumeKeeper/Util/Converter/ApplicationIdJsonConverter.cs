using VolumeKeeper.Models;

namespace VolumeKeeper.Util.Converter;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class ApplicationIdJsonConverter : JsonConverter<VolumeApplicationId>
{
    public override VolumeApplicationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        // Read the discriminator
        var path = root.GetString();
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new JsonException("Path cannot be null or empty.");
        }

        return new VolumeApplicationId(path);
    }

    public override void Write(Utf8JsonWriter writer, VolumeApplicationId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Path);
    }
}
