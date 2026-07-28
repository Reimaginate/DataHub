using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocument;

public class GetCosmosDocumentQuery<T> : IRequest<GetCosmosDocumentResponse<T>> where T : CosmosDocument
{
    public string Id { get; set; }
    public string Select { get; set; }
}
