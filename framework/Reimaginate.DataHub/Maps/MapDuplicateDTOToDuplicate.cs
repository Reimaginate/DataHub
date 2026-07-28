using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;
using Reimaginate.Mapper;

namespace Reimaginate.DataHub.Maps;

public class MapDuplicateDTOToDuplicate : ITypeMapper<DuplicateDTO, Duplicate>
{
    public Task<Duplicate> MapAsync(DuplicateDTO from, CancellationToken cancellationToken, Dictionary<string, object> cache = null)
    {
        var ret = new Duplicate()
        {
            id = from.DuplicateId,
            CreatedBy = from.CreatedBy,
            createdOn = from.CreatedOn,
            lastUpdated = from.LastUpdated ?? from.CreatedOn,
            Name = from.Name,
            EntityType = from.EntityType,
            EntityIds = from.EntityIds,
            LastUpdatedBy = from.LastUpdatedBy,
            MergePlan = from.MergePlan,
            Status = from.Status,
            FailureReason = from.FailureReason
        };

        return Task.FromResult(ret);
    }
}