using Newtonsoft.Json;

// ReSharper disable InconsistentNaming

namespace Reimaginate.DataHub.SharedModels.Core;

public class EntityReference
{
    public EntityReference()
    {}

    public EntityReference(string entityType, string entityId)
    {
        EntityType = entityType;
        EntityId = entityId;
    }

    [JsonProperty("@Tag")]
    public string _tag { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
  
}

public class EntityReference<T> : EntityReference where T : class
{
    public EntityReference()
    {
        EntityType = typeof(T).Name;
    }
}