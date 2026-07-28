namespace Reimaginate.DataHub.Config;

public class DataStoreOptions
{
    public string UseDatabase { get; set; } = "InMemory";
    public CosmosDbOptions CosmosDbOptions { get; set; } = new();
}