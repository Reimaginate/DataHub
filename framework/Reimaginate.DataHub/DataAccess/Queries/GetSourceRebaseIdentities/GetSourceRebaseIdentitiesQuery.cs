using System.Collections.Generic;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetSourceRebaseIdentities;

public sealed record SourceRebaseIdentity(string DataSource, string EntityType);

public class GetSourceRebaseIdentitiesQuery : IRequest<List<SourceRebaseIdentity>>
{
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
}
