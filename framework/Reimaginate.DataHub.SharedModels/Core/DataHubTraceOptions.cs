using System;

namespace Reimaginate.DataHub.SharedModels.Core;

public sealed class DataHubTraceOptions
{
    public bool Enabled { get; set; }
    public bool IncludeRequest { get; set; }
    public bool IncludeResponse { get; set; }
    public string Reason { get; set; }
    public DateTimeOffset? ExpiresOn { get; set; }
}
