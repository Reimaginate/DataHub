using Reimaginate.DataHub.VirtualTables.Abstractions.Core;

namespace Reimaginate.DataHub.VirtualTables.Abstractions.Requests;

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