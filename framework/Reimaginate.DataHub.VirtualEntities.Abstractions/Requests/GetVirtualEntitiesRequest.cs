using Reimaginate.DataHub.VirtualEntities.Abstractions.Core;

namespace Reimaginate.DataHub.VirtualEntities.Abstractions.Requests;

public class GetVirtualEntitiesRequest : VirtualEntitiesRequest<GetVirtualEntitiesResponse>
{
    public GetVirtualEntitiesRequest()
    {
        RequestType = nameof(GetVirtualEntitiesRequest);
    }

    public GetVirtualEntitiesRequest(string queryExpression) : this()
    {
        QueryExpression = queryExpression;
    }

    public string QueryExpression { get; set; } = null!;
}