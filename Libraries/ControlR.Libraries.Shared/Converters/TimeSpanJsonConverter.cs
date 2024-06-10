﻿using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Converters;

public class TimeSpanJsonConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var stringValue = reader.GetString();
        if (TimeSpan.TryParse(stringValue, out var result))
        {
            return result;
        }

        throw new ArgumentException("Failed to convert to TimeSpan.");
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}