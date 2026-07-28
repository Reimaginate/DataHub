using System;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Core;

public sealed class LogEntry : CosmosDocument
{
    public LogEntry()
    {
        _dt = nameof(LogEntry);
    }
    public string Type { get; set; }
    public JObject Data { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}