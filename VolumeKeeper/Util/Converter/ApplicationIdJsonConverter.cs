using VolumeKeeper.Models;

namespace VolumeKeeper.Util.Converter;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public class ApplicationIdJsonConverter : JsonConverter<VolumeApplicationId>
{
    public override VolumeApplicationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        // Read the discriminator
        var matchType = root.GetProperty("NameMatchType").GetString();

        return matchType switch
        {
            "Name" => new NamedVolumeApplicationId(root.GetProperty("Name").GetString()!),
            "Path" => new PathVolumeApplicationId(root.GetProperty("Path").GetString()!),
            _ => throw new JsonException("Unknown NameMatchType")
        };
    }

    public override void Write(Utf8JsonWriter writer, VolumeApplicationId value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("NameMatchType", value.NameMatchType.ToString());

        switch (value)
        {
            case NamedVolumeApplicationId named:
                writer.WriteString("Name", named.Name);
                break;
            case PathVolumeApplicationId path:
                writer.WriteString("Path", path.Path);
                break;
        }

        writer.WriteEndObject();
    }
