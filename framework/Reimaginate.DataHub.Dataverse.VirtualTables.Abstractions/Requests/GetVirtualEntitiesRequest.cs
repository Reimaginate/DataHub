using Reimaginate.DataHub.Dataverse.VirtualTables.Abstractions.Core;

namespace Reimaginate.DataHub.Dataverse.VirtualTables.Abstractions.Requests;

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