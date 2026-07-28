using System.Collections.Generic;
using Reimaginate.DataHub;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.SharedModels.Core;

public abstract class DataHubCLIRequest : IRequest
{
    public string CorrelationId { get; set; }
    public string RequestType { get; set; }
    public IReadOnlyCollection<DataHubQueryParameter> Parameters { get; set; }
    public DataHubTraceOptions TraceOptions { get; set; }

    public User User { get; set; }
}

public abstract class DataHubCLIRequest<TResponse> : DataHubCLIRequest, IRequest<TResponse>
{

}
