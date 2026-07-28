using System.Dynamic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.TestFramework.Helpers;

public static class ExpandoObjectExtensions
{
    public static JObject? Reserialize(ExpandoObject value)
    {
        var serializerSettings = new JsonSerializerSettings()
        {
            DateParseHandling = DateParseHandling.DateTimeOffset
        };

        var deserializerSettings = new JsonSerializerSettings()
        {
            DateParseHandling = DateParseHandling.DateTimeOffset,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Error = (sender, error) =>
            {
                error.ErrorContext.Handled = true;
            }
        };

        var serializedValue = JsonConvert.SerializeObject(value, serializerSettings);
        var deserializedValue = JsonConvert.DeserializeObject<JObject>(serializedValue, deserializerSettings);
        return deserializedValue;
    }
}