namespace Reimaginate.DataHub.Diagnostics;

public class DataHubTraceQueryOptions
{
    public const int DefaultDefaultLookbackHours = 24;
    public const int DefaultMaxLookbackHours = 168;
    public const int DefaultMaxResultCount = 1000;
    public const int DefaultDefaultResultCount = 500;

    public string LogsWorkspaceId { get; set; }
    public string LogsResourceId { get; set; }
    public int DefaultLookbackHours { get; set; } = DefaultDefaultLookbackHours;
    public int MaxLookbackHours { get; set; } = DefaultMaxLookbackHours;
    public int MaxResultCount { get; set; } = DefaultMaxResultCount;
    public int DefaultResultCount { get; set; } = DefaultDefaultResultCount;
}
