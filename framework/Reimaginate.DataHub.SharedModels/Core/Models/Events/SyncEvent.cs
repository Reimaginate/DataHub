namespace Reimaginate.DataHub.SharedModels.Core.Models.Events;

public class SyncEvent : Event
{
    public string DataSource { get; set; }
    public string AgentId { get; set; }
    public string SourceEntityType { get; set; }
    public string SourceEntityId { get; set; }
    public string DataHubEntityType { get; set; }
    public string DataHubEntityId { get; set; }
    public string Description { get; set; }

}