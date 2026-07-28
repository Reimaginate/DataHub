using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.Mapper;

namespace Reimaginate.DataHub.Maps;

public class MapJobToJobDTO : ITypeMapper<Job, JobDTO>
{
    public Task<JobDTO> MapAsync(Job from, CancellationToken cancellationToken, Dictionary<string, object> cache = null)
    {
        var ret = new JobDTO()
        {
            JobId = from.id,
            CompletedOn = from.CompletedOn,
            CreatedBy = from.CreatedBy,
            CreatedOn = from.createdOn,
            LastUpdated = from.lastUpdated,
            Name = from.Name,
            Request = from.Request,
            Response = from.Response,
            Status = from.Status,
            Target = from.Target,
            Type = from.Type,
        };

        return Task.FromResult(ret);
    }
}