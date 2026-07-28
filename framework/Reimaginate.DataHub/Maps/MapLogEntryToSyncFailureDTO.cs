using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.Mapper;

namespace Reimaginate.DataHub.Maps;

public class MapLogEntryToSyncFailureDTO : ITypeMapper<LogEntry, SyncFailureDTO>
{
    public Task<SyncFailureDTO> MapAsync(LogEntry from, CancellationToken cancellationToken, Dictionary<string, object> cache = null)
    {
        if (from.Data == null)
        {
            return Task.FromResult((SyncFailureDTO)null);
        }

        var syncFailure = from.Data.ToObject<SyncFailure>();

        var ret = new SyncFailureDTO()
        {
            Id = from.id,
            AgentId = syncFailure.AgentId,
            DataHubEntityId = syncFailure.DataHubEntityId,
            DataHubEntityType = syncFailure.DataHubEntityType,
            DataSource = syncFailure.DataSource,
            Description = syncFailure.Description,
            FailureReason = syncFailure.FailureReason,
            FailureType = syncFailure.FailureType,
            SourceEntityId = syncFailure.SourceEntityId,
            SourceEntityType = syncFailure.SourceEntityType,
            Timestamp = syncFailure.Timestamp
        };

        return Task.FromResult(ret);
    }
}