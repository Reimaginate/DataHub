using System;

namespace Reimaginate.DataHub.AspNetCore.Readiness;

public sealed class DataHubReadinessOptions
{
    public string ServiceName { get; set; } = "DataHub";
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromSeconds(30);
    public bool CheckCosmosConnectivity { get; set; } = true;
    public bool CheckProcessingLocks { get; set; } = true;
    public bool CheckEventGridConfiguration { get; set; } = true;
}
