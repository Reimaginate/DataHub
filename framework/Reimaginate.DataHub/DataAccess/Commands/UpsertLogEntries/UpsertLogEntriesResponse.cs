using System.Collections.Generic;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.DataAccess.Commands.UpsertLogEntries;

public class UpsertLogEntriesResponse
{
    public List<LogEntry> Successes { get; set; }

    public List<DataAccessFailure<LogEntry>> Failures { get; set; }
}