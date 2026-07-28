namespace Reimaginate.DataHub.SharedModels.Core;

public abstract class CosmosDocument : PartitionedDataDocument
{
    public long _ts { get; set; }
    public string _etag { get; set; }
    public string _dt { get; set; }
      
}
