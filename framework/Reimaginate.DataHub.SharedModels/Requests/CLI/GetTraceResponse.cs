using System;
using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetTraceResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public string CorrelationId { get; set; }
    public DateTimeOffset FromUtc { get; set; }
    public DateTimeOffset ToUtc { get; set; }
    public int Limit { get; set; }
    public List<TraceRecord> Records { get; set; } = new();
}

public class TraceRecord
{
    public string OperationId { get; set; }
    public string SpanId { get; set; }
    public string ParentSpanId { get; set; }
    public string Kind { get; set; }
    public string Name { get; set; }
    public string Message { get; set; }
    public DateTimeOffset StartTimeUtc { get; set; }
    public double? DurationMs { get; set; }
    public bool? Success { get; set; }
    public string Severity { get; set; }
    public string CloudRoleName { get; set; }
    public string RequestType { get; set; }
    public string ErrorId { get; set; }
    public string ErrorCategory { get; set; }
    public int? HttpStatusCode { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
    public Dictionary<string, string> Details { get; set; } = new();
}
