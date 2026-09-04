using System.Text.Json;
using System.Text.Json.Serialization;

namespace MystTiq.Core.Automation;

// AutomationRuleId is a single-value wrapper struct; without this converter System.Text.Json
// would serialize it as a nested {"value":"..."} object instead of a bare string, breaking the
// "IDs are plain strings" convention every other DTO in this app follows (see OperationId's
// converter in MystTiq.Core.Operations for the same rationale).
public sealed class AutomationRuleIdJsonConverter : JsonConverter<AutomationRuleId>
{
    public override AutomationRuleId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, AutomationRuleId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
