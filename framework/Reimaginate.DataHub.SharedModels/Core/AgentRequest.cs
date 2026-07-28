using Reimaginate.Mediator;

namespace Reimaginate.DataHub.SharedModels.Core;

public class AgentRequest : IRequest
{
    public string RequestType { get; set; }
    public string JobId { get; set; }
}

public class AgentRequest<TResponse> : AgentRequest, IRequest<TResponse>
{

}