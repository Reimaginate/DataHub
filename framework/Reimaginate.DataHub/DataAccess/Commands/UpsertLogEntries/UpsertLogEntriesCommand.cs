using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

// ReSharper disable IdentifierTypo

namespace Reimaginate.DataHub.DataAccess.Commands.UpsertLogEntries;

public class UpsertLogEntriesCommand : IRequest<UpsertLogEntriesResponse>
{
    public List<LogEntry> LogEntries { get; set; }
}