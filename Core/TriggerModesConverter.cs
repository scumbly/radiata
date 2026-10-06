using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControllerWheel;

/// <summary>JSON converter for <see cref="SystemConfig.TriggerModes"/> (per-controller-kind → the SET of
/// enabled invocation gestures). Tolerates the LEGACY scalar form <c>{"DualSenseEdge":"fn"}</c> by reading
/// a string value as a one-element list, so upgrading a config written before multi-select doesn't fail to
/// parse (which would trip ConfigLoader's corrupt-file backup + defaults reset). Always writes arrays.</summary>
public sealed class TriggerModesConverter : JsonConverter<Dictionary<string, List<string>>>
{
    public override Dictionary<string, List<string>> Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var map = new Dictionary<string, List<string>>();
        if (reader.TokenType == JsonTokenType.Null) return map;
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("triggerModes: expected an object");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) return map;
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException();
            string key = reader.GetString()!;
            reader.Read();

            var list = new List<string>();
            switch (reader.TokenType)
            {
                case JsonTokenType.String:                          // legacy scalar: one gesture
                    if (reader.GetString() is { } scalar && !string.IsNullOrWhiteSpace(scalar)) list.Add(scalar);
                    break;
                case JsonTokenType.StartArray:                      // current form: a set of gestures
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } s
                            && !string.IsNullOrWhiteSpace(s) && !list.Contains(s))
                            list.Add(s);
                    break;
                case JsonTokenType.Null:
                    break;                                          // no gestures for this kind
                default:
                    throw new JsonException("triggerModes: expected a string or array value");
            }
            map[key] = list;
        }
        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, List<string>> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (key, list) in value)
        {
            writer.WritePropertyName(key);
            writer.WriteStartArray();
            foreach (var token in list) writer.WriteStringValue(token);
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }
}
