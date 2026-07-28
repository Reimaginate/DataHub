using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.GetUser;

public class GetUserRequest : IRequest<GetUserResponse>
{
    public string TenantId { get; set; }
    public string EntraObjectId { get; set; }
    public string UserId { get; set; }
}
