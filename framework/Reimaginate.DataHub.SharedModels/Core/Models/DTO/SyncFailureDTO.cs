using System;

namespace Reimaginate.DataHub.SharedModels.Core.Models.DTO;

public class SyncFailureDTO
{
    public string Id { get; set; }
    public string DataSource { get; set; }
    public string AgentId { get; set; }
    public string SourceEntityType { get; set; }
    public string SourceEntityId { get; set; }
    public string DataHubEntityType { get; set; }
    public string DataHubEntityId { get; set; }
    public string Description { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string FailureType { get; set; }
    public string FailureReason { get; set; }
}