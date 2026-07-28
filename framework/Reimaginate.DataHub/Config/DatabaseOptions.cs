using System;

namespace Reimaginate.DataHub.Config;

public class DatabaseOptions
{
    public Func<AddDataHubServiceOptions> UseInMemoryDatabase { get; set; }
    public Func<Action<CosmosDbOptions>, AddDataHubServiceOptions> UseCosmosDatabase { get; set; }
}