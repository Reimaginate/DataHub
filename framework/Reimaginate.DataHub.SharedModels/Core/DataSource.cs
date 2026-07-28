using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Core;

public class DataSource : CosmosDocument
{
    public DataSource()
    {
        _dt = nameof(DataSource);
    }

    public string Name { get; set; }

    public string Description { get; set; }

    public List<Agent> Agents { get; set; } = new();
}