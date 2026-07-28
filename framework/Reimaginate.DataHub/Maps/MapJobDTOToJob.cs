using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.Mapper;

namespace Reimaginate.DataHub.Maps;

public class MapJobDTOToJob : ITypeMapper<JobDTO, Job>
{
    public Task<Job> MapAsync(JobDTO from, CancellationToken cancellationToken, Dictionary<string, object> cache = null)
    {
        var ret = new Job()
        {
            id = from.JobId,
            CompletedOn = from.CompletedOn,
            CreatedBy = from.CreatedBy,
            createdOn = from.CreatedOn,
            lastUpdated = from.LastUpdated ?? from.CreatedOn,
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