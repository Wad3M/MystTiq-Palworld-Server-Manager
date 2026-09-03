using System.Text.Json;
using System.Text.Json.Serialization;

namespace MystTiq.Core.Operations;

// OperationId/ServerProfileId are single-value wrapper structs; without these
// converters System.Text.Json would serialize them as a nested {"value":"..."}
// object instead of a bare string, breaking the "IDs are plain strings"
// convention every other DTO in this app already follows.
public sealed class OperationIdJsonConverter : JsonConverter<OperationId>
{
    public override OperationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, OperationId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

public sealed class ServerProfileIdJsonConverter : JsonConverter<ServerProfileId>
{
    public override ServerProfileId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, ServerProfileId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
