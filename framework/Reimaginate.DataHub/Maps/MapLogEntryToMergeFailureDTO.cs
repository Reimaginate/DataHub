using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.Mapper;

namespace Reimaginate.DataHub.Maps;

public class MapLogEntryToMergeFailureDTO : ITypeMapper<LogEntry,MergeFailureDTO>
{
    public Task<MergeFailureDTO> MapAsync(LogEntry from, CancellationToken cancellationToken, Dictionary<string, object> cache = null)
    {
        if (from.Data == null)
        {
            return Task.FromResult((MergeFailureDTO)null);
        }

        var mergeFailure = from.Data.ToObject<MergeFailure>();

        var ret = new MergeFailureDTO()
        {
            Id = from.id,
            AgentId = mergeFailure.AgentId,
            DataHubEntityId = mergeFailure.DataHubEntityId,
            DataHubEntityType = mergeFailure.DataHubEntityType,
            DataSource = mergeFailure.DataSource,
            Description = mergeFailure.Description,
            FailureReason = mergeFailure.FailureReason,
            FailureType = mergeFailure.FailureType,
            SourceEntityId = mergeFailure.SourceEntityId,
            SourceEntityType = mergeFailure.SourceEntityType,
            Timestamp = mergeFailure.Timestamp
        };

        return Task.FromResult(ret);
    }
}