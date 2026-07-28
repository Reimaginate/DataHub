using System;
using System.IO;
using System.Text;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Constants;

namespace Reimaginate.DataHub.Helpers;

public sealed class DataHubDataSerializer : CosmosSerializer
{
    private const string EscapedNullChar = "\\u0000";

    private readonly JsonSerializer _serializer = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DateFormatString = DateFormats.ISO8601,
        DateParseHandling = DateParseHandling.None,
        Converters = { new DateTimeOffsetJsonConverter(), new NullableDateTimeOffsetJsonConverter() }
    };

    public override T FromStream<T>(Stream stream)
    {
        using var sr = new StreamReader(stream);
        using var tr = new JsonTextReader(sr);
        tr.DateParseHandling = DateParseHandling.None;
        var token = JToken.ReadFrom(tr);
        RestoreNullChars(token);
        return token.ToObject<T>(_serializer);
    }

    public override Stream ToStream<T>(T input)
    {
        using var stringWriter = new StringWriter();
        using var jsonTextWriter = new JsonTextWriter(stringWriter);
        var token = JToken.FromObject(input, _serializer);
        EscapeNullChars(token);
        token.WriteTo(jsonTextWriter);
        var content = stringWriter.ToString();
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    private static void EscapeNullChars(JToken token)
    {
        switch (token)
        {
            case JObject obj:
                foreach (var property in obj.Properties())
                {
                    EscapeNullChars(property.Value);
                }

                break;

            case JArray array:
                foreach (var item in array)
                {
                    EscapeNullChars(item);
                }

                break;

            case JValue { Type: JTokenType.String } value when value.Value is string str && str.Contains('\0'):
                value.Value = str.Replace("\0", EscapedNullChar, StringComparison.Ordinal);
                break;
        }
    }

    private static void RestoreNullChars(JToken token)
    {
        switch (token)
        {
            case JObject obj:
                foreach (var property in obj.Properties())
                {
                    RestoreNullChars(property.Value);
                }

                break;

            case JArray array:
                foreach (var item in array)
                {
                    RestoreNullChars(item);
                }

                break;

            case JValue { Type: JTokenType.String } value when value.Value is string str && str.Contains(EscapedNullChar, StringComparison.Ordinal):
                value.Value = str.Replace(EscapedNullChar, "\0", StringComparison.Ordinal);
                break;
        }
    }
}
