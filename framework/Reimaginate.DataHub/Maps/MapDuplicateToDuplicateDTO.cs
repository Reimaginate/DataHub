using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;
using Reimaginate.Mapper;

namespace Reimaginate.DataHub.Maps;

public class MapDuplicateToDuplicateDTO : ITypeMapper<Duplicate, DuplicateDTO>
{
    public Task<DuplicateDTO> MapAsync(Duplicate from, CancellationToken cancellationToken, Dictionary<string, object> cache = null)
    {
        var ret = new DuplicateDTO()
        {
            DuplicateId = from.id,
            CreatedBy = from.CreatedBy,
            CreatedOn = from.createdOn,
            LastUpdated = from.lastUpdated,
            Name = from.Name,
            Status = from.Status,
            EntityType = from.EntityType,
            EntityIds = from.EntityIds,
            LastUpdatedBy = from.LastUpdatedBy,
            MergePlan = from.MergePlan,
            FailureReason = from.FailureReason

        };

        return Task.FromResult(ret);
    }
}