using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.AspNetCore.Observability;

public static class DataHubTelemetryPayloadSanitizer
{
    private const string RedactedValue = "[redacted]";

    public static string Sanitize(string payload, DataHubObservabilityOptions options)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        ArgumentNullException.ThrowIfNull(options);

        string sanitized;
        try
        {
            var token = JToken.Parse(payload);
            Redact(token, new HashSet<string>(options.RedactedPropertyNames ?? [], StringComparer.OrdinalIgnoreCase));
            sanitized = token.ToString(Formatting.None);
        }
        catch (JsonException)
        {
            sanitized = payload;
        }

        var maxLength = options.MaxTelemetryPayloadCharacters <= 0
            ? 4096
            : options.MaxTelemetryPayloadCharacters;

        return sanitized.Length <= maxLength
            ? sanitized
            : sanitized[..maxLength] + "...[truncated]";
    }

    private static void Redact(JToken token, ISet<string> redactedNames)
    {
        if (token is JObject obj)
        {
            foreach (var property in obj.Properties().ToList())
            {
                if (redactedNames.Contains(property.Name))
                {
                    property.Value = RedactedValue;
                    continue;
                }

                Redact(property.Value, redactedNames);
            }

            return;
        }

        if (token is JArray array)
        {
            foreach (var child in array)
            {
                Redact(child, redactedNames);
            }
        }
    }
}
