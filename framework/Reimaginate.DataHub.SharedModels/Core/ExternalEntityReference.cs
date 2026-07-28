using System;

namespace Reimaginate.DataHub.SharedModels.Core;

public class ExternalEntityReference : EntityReference
{
    public ExternalEntityReference()
    {
        _tag = nameof(ExternalEntityReference);
    }

    public string DataSource { get; set; }
    public string SourceEntityType { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
}

public class ExternalEntityReference<TSrc, TDataHub> : EntityReference<TDataHub> where TSrc : class where TDataHub : class
{
    public ExternalEntityReference()
    {
        _tag = nameof(ExternalEntityReference);

        EntityType = typeof(TDataHub).Name;
        SourceEntityType = typeof(TSrc).Name;
    }

    public string DataSource { get; set; }
    public string SourceEntityType { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
}