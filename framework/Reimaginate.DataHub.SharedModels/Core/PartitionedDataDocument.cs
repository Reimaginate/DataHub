namespace Reimaginate.DataHub.SharedModels.Core;

public abstract class PartitionedDataDocument : DataDocument
{
    public string pk { get; set; }
}