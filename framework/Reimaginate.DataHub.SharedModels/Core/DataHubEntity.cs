using System;
using System.Collections.Generic;

// ReSharper disable InconsistentNaming

namespace Reimaginate.DataHub.SharedModels.Core;

public class DataHubEntityBase : CosmosDocument
{
    public DataHubEntityBase()
    {
        _dt = nameof(DataHubEntity);
    }

    public string entityType { get; set; }

    public DateTimeOffset? createdOn { get; set; }

    public DateTimeOffset? lastUpdated { get; set; }

    public List<AlternateKey> alternateKeys { get; set; }
}

public class DataHubEntity : DataHubEntityBase
{
    public bool? noSync { get; set; }
    public List<string> syncWhitelist { get; set; }
    public List<string> syncBlacklist { get; set; }
}