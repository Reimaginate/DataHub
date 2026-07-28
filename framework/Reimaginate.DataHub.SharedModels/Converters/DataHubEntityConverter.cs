using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Converters;

internal sealed class DataHubEntityConverter : JsonConverter<JObject>
{
    public override void WriteJson(JsonWriter writer, JObject value, JsonSerializer serializer)
    {
        value.WriteTo(writer);
    }
    public override JObject ReadJson(JsonReader reader, Type objectType, JObject existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        reader.DateParseHandling = DateParseHandling.DateTimeOffset;
        var j = serializer.Deserialize<JObject>(reader);
        return j;
    }
}