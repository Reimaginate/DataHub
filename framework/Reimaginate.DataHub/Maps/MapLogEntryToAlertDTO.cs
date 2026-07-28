using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.DTO;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;
using Reimaginate.Mapper;

namespace Reimaginate.DataHub.Maps;

public class MapLogEntryToAlertDTO : ITypeMapper<LogEntry, AlertDTO>
{
    public Task<AlertDTO> MapAsync(LogEntry from, CancellationToken cancellationToken, Dictionary<string, object> cache = null)
    {
        if (from.Data == null)
        {
            return Task.FromResult((AlertDTO)null);
        }

        var alert = from.Data.ToObject<Alert>();

        var ret = new AlertDTO()
        {
            Id = from.id,
            Data = alert.Data,
            Description = alert.Description,
            Severity = alert.Severity,
            Timestamp = alert.Timestamp,
            Subject = alert.Subject
        };

        return Task.FromResult(ret);
    }
}