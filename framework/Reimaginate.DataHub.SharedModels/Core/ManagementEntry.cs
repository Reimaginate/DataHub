using System;
// ReSharper disable InconsistentNaming

namespace Reimaginate.DataHub.SharedModels.Core;

public abstract class ManagementEntry : CosmosDocument
{
    public DateTimeOffset createdOn { get; set; }
    public DateTimeOffset lastUpdated { get; set; }
}