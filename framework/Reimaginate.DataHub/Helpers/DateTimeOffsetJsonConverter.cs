using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Constants;

namespace Reimaginate.DataHub.Helpers;

public class NullableDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset?>
{
    public override void WriteJson(JsonWriter writer, DateTimeOffset? value, JsonSerializer serializer)
    {
        var val = (DateTimeOffset?)value;
        if (val != null)
        {
            if (val.Value.Offset == new TimeSpan(0, 0, 0))
            {
                val = val.Value.ToOffset(new TimeSpan(10, 0, 0));
            }
        }

        var t = JToken.FromObject(val!, new JsonSerializer()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatString = DateFormats.ISO8601
        });
        t.WriteTo(writer);
    }

    public override DateTimeOffset? ReadJson(JsonReader reader, Type objectType, DateTimeOffset? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    public override bool CanRead => false;
}

public class DateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override void WriteJson(JsonWriter writer, DateTimeOffset value, JsonSerializer serializer)
    {
        if (value.Offset == new TimeSpan(0, 0, 0))
        {
            value = value.ToOffset(new TimeSpan(10, 0, 0));
        }

        var t = JToken.FromObject(value!, new JsonSerializer()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatString = DateFormats.ISO8601
        });
        t.WriteTo(writer);
    }

    public override DateTimeOffset ReadJson(JsonReader reader, Type objectType, DateTimeOffset existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    public override bool CanRead => false;
}