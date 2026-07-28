using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.Mapper;

namespace Reimaginate.DataHub.Maps;

public class MapLogEntryToPatchFailureDTO : ITypeMapper<LogEntry, PatchFailureDTO>
{
    public Task<PatchFailureDTO> MapAsync(LogEntry from, CancellationToken cancellationToken, Dictionary<string, object> cache = null)
    {
        if (from.Data == null)
        {
            return Task.FromResult((PatchFailureDTO)null);
        }

        var patchFailure = from.Data.ToObject<PatchFailure>();

        return Task.FromResult(new PatchFailureDTO
        {
            Id = from.id,
            Timestamp = patchFailure.Timestamp,
            EventSource = patchFailure.EventSource,
            DataSource = patchFailure.DataSource,
            EntityType = patchFailure.EntityType,
            EntityId = patchFailure.EntityId,
            Patch = patchFailure.Patch,
            FailureReason = patchFailure.FailureReason
        });
    }
}
